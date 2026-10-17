using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.ProductGroups;

// ─── REMOVE PRODUCT FROM GROUP ──────────────────────────────────────────────
public record RemoveProductFromGroupCommand(Guid BatchId, Guid ProductId) : IRequest<Result>;

public class RemoveProductFromGroupCommandHandler : IRequestHandler<RemoveProductFromGroupCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public RemoveProductFromGroupCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(RemoveProductFromGroupCommand request, CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can manage group listings.", "FORBIDDEN");

        var group = await _context.ProductGroupListings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == request.BatchId && g.DeletedAt == null, cancellationToken);

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => 
                (p.Id == request.ProductId || p.SourceProductId == request.ProductId) &&
                (p.ListingBatchId == request.BatchId || (group != null && p.ShopId == group.ShopId && (p.ListingBatchId == group.Id || p.BatchName == group.BatchName))) &&
                p.DeletedAt == null, 
                cancellationToken);

        if (product is null && group != null)
        {
            // Fallback: check if the product belongs to this group's shop with this ID
            product = await _context.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => (p.Id == request.ProductId || p.SourceProductId == request.ProductId) && p.ShopId == group.ShopId && p.DeletedAt == null, cancellationToken);
        }

        if (product is null)
            return Result.Failure("Product not found in this group.", "PRODUCT_NOT_FOUND");

        var now = _dateTime.UtcNow;
        product.DeletedAt = now;
        product.IsActive = false;
        product.ListingBatchId = null;
        product.SetUpdatedAt(now);

        // Also soft-delete product variants
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(v => v.ProductId == product.Id && v.DeletedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var v in variants)
        {
            v.DeletedAt = now;
            v.SetUpdatedAt(now);
        }

        // Decrement group delivered count
        if (group != null && group.DeliveredCount > 0)
        {
            group.DeliveredCount--;
            group.SetUpdatedAt(now);
        }

        // Decrement shop total product count
        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);
        if (shop is not null && shop.TotalProducts > 0)
        {
            shop.TotalProducts--;
            shop.SetUpdatedAt(now);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ─── REPLACE PRODUCT IN GROUP ───────────────────────────────────────────────
public record ReplaceProductInGroupCommand(
    Guid BatchId,
    Guid OldProductId,
    Guid NewWarehouseProductId
) : IRequest<Result<GroupProductCardDto>>;

public class ReplaceProductInGroupCommandHandler : IRequestHandler<ReplaceProductInGroupCommand, Result<GroupProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ReplaceProductInGroupCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<GroupProductCardDto>> Handle(ReplaceProductInGroupCommand request, CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<GroupProductCardDto>("Only admins can manage group listings.", "FORBIDDEN");

        var group = await _context.ProductGroupListings
            .FirstOrDefaultAsync(g => g.Id == request.BatchId && g.DeletedAt == null, cancellationToken);

        if (group is null)
            return Result.Failure<GroupProductCardDto>("Product group not found.", "GROUP_NOT_FOUND");

        var oldProduct = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.OldProductId && p.ListingBatchId == request.BatchId && p.DeletedAt == null, cancellationToken);

        if (oldProduct is null)
            return Result.Failure<GroupProductCardDto>("Original product not found in this group.", "PRODUCT_NOT_FOUND");

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == group.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure<GroupProductCardDto>("Shop not found.", "SHOP_NOT_FOUND");

        var newWarehouseProduct = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.NewWarehouseProductId && p.DeletedAt == null && p.IsActive, cancellationToken);

        if (newWarehouseProduct is null)
            return Result.Failure<GroupProductCardDto>("New warehouse product not found.", "WAREHOUSE_PRODUCT_NOT_FOUND");

        // Remove old product
        var now = _dateTime.UtcNow;
        oldProduct.DeletedAt = now;
        oldProduct.IsActive = false;
        oldProduct.SetUpdatedAt(now);

        // Clone new product
        var slug = await ProductSlug.GenerateUniqueAsync(_context, newWarehouseProduct.Name, prefix: shop.Slug, cancellationToken);
        var replacement = new Product
        {
            ShopId = shop.Id,
            SourceProductId = newWarehouseProduct.Id,
            ListingBatchId = group.Id,
            BatchName = group.BatchName,
            CategoryId = newWarehouseProduct.CategoryId,
            BrandId = newWarehouseProduct.BrandId,
            Name = newWarehouseProduct.Name,
            Slug = slug,
            Description = newWarehouseProduct.Description,
            BasePrice = newWarehouseProduct.BasePrice,
            IsFeatured = false,
            IsApproved = true, // Admin replaced
            ViewCount = 0,
            SeoTitle = newWarehouseProduct.SeoTitle,
            SeoDescription = newWarehouseProduct.SeoDescription,
            IsActive = true
        };
        replacement.SetCreatedAt(now);
        replacement.SetUpdatedAt(now);
        _context.Products.Add(replacement);
        await _context.SaveChangesAsync(cancellationToken);

        await MasterCatalog.CloneImagesAsync(_context, newWarehouseProduct.Id, replacement.Id, cancellationToken);
        await MasterCatalog.CloneVariantsAsync(_context, newWarehouseProduct.Id, replacement.Id, now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Get cover image
        var img = await _context.ProductImages
            .Where(pi => pi.ProductId == replacement.Id)
            .OrderByDescending(pi => pi.IsCover)
            .Select(pi => pi.ImageUrl)
            .FirstOrDefaultAsync(cancellationToken);

        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == replacement.CategoryId, cancellationToken);
        var brand = replacement.BrandId.HasValue
            ? await _context.Brands.FirstOrDefaultAsync(b => b.Id == replacement.BrandId.Value, cancellationToken)
            : null;
        var variantCount = await _context.ProductVariants.CountAsync(pv => pv.ProductId == replacement.Id && pv.DeletedAt == null, cancellationToken);

        return Result.Success(new GroupProductCardDto(
            replacement.Id,
            replacement.SourceProductId,
            replacement.Name,
            replacement.Slug,
            replacement.BasePrice,
            img,
            replacement.CategoryId,
            cat?.Name ?? "Category",
            replacement.BrandId,
            brand?.Name ?? "Brand",
            replacement.IsApproved,
            replacement.IsActive,
            variantCount,
            replacement.CreatedAt
        ));
    }
}

// ─── ADD PRODUCTS TO GROUP ──────────────────────────────────────────────────
public record AddProductsToGroupCommand(
    Guid BatchId,
    int AdditionalCount = 0,
    List<Guid>? SpecificWarehouseProductIds = null
) : IRequest<Result<int>>;

public class AddProductsToGroupCommandHandler : IRequestHandler<AddProductsToGroupCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AddProductsToGroupCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<int>> Handle(AddProductsToGroupCommand request, CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<int>("Only admins can manage group listings.", "FORBIDDEN");

        var group = await _context.ProductGroupListings
            .FirstOrDefaultAsync(g => g.Id == request.BatchId && g.DeletedAt == null, cancellationToken);

        if (group is null)
            return Result.Failure<int>("Product group not found.", "GROUP_NOT_FOUND");

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == group.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure<int>("Shop not found.", "SHOP_NOT_FOUND");

        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);

        var existingInShop = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ShopId == shop.Id && p.DeletedAt == null)
            .Select(p => p.SourceProductId ?? p.Id)
            .ToListAsync(cancellationToken);
        var existingSet = existingInShop.ToHashSet();

        List<Product> productsToClone = [];

        if (request.SpecificWarehouseProductIds != null && request.SpecificWarehouseProductIds.Count > 0)
        {
            productsToClone = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => request.SpecificWarehouseProductIds.Contains(p.Id) && p.DeletedAt == null && !existingSet.Contains(p.Id))
                .ToListAsync(cancellationToken);
        }
        else if (request.AdditionalCount > 0)
        {
            var candidates = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => p.ShopId == masterShop.Id && p.SourceProductId == null && p.IsActive && p.IsApproved && p.DeletedAt == null && !existingSet.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var rand = new Random();
            var pickIds = candidates.OrderBy(_ => rand.Next()).Take(request.AdditionalCount).ToList();

            productsToClone = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => pickIds.Contains(p.Id))
                .ToListAsync(cancellationToken);
        }

        if (productsToClone.Count == 0)
            return Result.Failure<int>("No new warehouse products available to add to this group.", "NO_AVAILABLE_PRODUCTS");

        var now = _dateTime.UtcNow;
        int added = 0;
        foreach (var source in productsToClone)
        {
            var slug = await ProductSlug.GenerateUniqueAsync(_context, source.Name, prefix: shop.Slug, cancellationToken);
            var listing = new Product
            {
                ShopId = shop.Id,
                SourceProductId = source.Id,
                ListingBatchId = group.Id,
                BatchName = group.BatchName,
                CategoryId = source.CategoryId,
                BrandId = source.BrandId,
                Name = source.Name,
                Slug = slug,
                Description = source.Description,
                BasePrice = source.BasePrice,
                IsFeatured = false,
                IsApproved = true,
                ViewCount = 0,
                SeoTitle = source.SeoTitle,
                SeoDescription = source.SeoDescription,
                IsActive = true
            };
            listing.SetCreatedAt(now);
            listing.SetUpdatedAt(now);
            _context.Products.Add(listing);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await MasterCatalog.CloneImagesAsync(_context, source.Id, listing.Id, cancellationToken);
                await MasterCatalog.CloneVariantsAsync(_context, source.Id, listing.Id, now, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                added++;
            }
            catch
            {
                // Continue
            }
        }

        group.DeliveredCount += added;
        shop.TotalProducts += added;
        group.SetUpdatedAt(now);
        shop.SetUpdatedAt(now);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(added);
    }
}

// ─── DELETE PRODUCT GROUP ───────────────────────────────────────────────────
public record DeleteProductGroupCommand(Guid BatchId) : IRequest<Result>;

public class DeleteProductGroupCommandHandler : IRequestHandler<DeleteProductGroupCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteProductGroupCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(DeleteProductGroupCommand request, CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can manage group listings.", "FORBIDDEN");

        var group = await _context.ProductGroupListings
            .FirstOrDefaultAsync(g => g.Id == request.BatchId && g.DeletedAt == null, cancellationToken);

        if (group is null)
            return Result.Failure("Product group not found.", "GROUP_NOT_FOUND");

        var now = _dateTime.UtcNow;
        group.DeletedAt = now;
        group.SetUpdatedAt(now);

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ListingBatchId == group.Id && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var p in products)
        {
            p.DeletedAt = now;
            p.IsActive = false;
            p.SetUpdatedAt(now);
        }

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == group.ShopId, cancellationToken);
        if (shop is not null)
        {
            shop.TotalProducts = Math.Max(0, shop.TotalProducts - products.Count);
            shop.SetUpdatedAt(now);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ─── APPROVE PRODUCT GROUP / BATCH ──────────────────────────────────────────
public record ApproveProductGroupCommand(Guid BatchId) : IRequest<Result<int>>;

public class ApproveProductGroupCommandHandler : IRequestHandler<ApproveProductGroupCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApproveProductGroupCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<int>> Handle(ApproveProductGroupCommand request, CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<int>("Only admins can approve group listings.", "FORBIDDEN");

        var group = await _context.ProductGroupListings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == request.BatchId && g.DeletedAt == null, cancellationToken);

        if (group is null)
            return Result.Failure<int>("Product group not found.", "GROUP_NOT_FOUND");

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ListingBatchId == group.Id && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var now = _dateTime.UtcNow;
        int approvedCount = 0;
        foreach (var p in products)
        {
            if (!p.IsApproved || !p.IsActive)
            {
                p.Approve();
                p.SetUpdatedAt(now);
                approvedCount++;
            }
        }

        group.SetUpdatedAt(now);

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == group.ShopId, cancellationToken);
        if (shop is not null)
        {
            var activeCount = await _context.Products
                .IgnoreQueryFilters()
                .CountAsync(p => p.ShopId == shop.Id && p.DeletedAt == null && p.IsApproved, cancellationToken);
            shop.TotalProducts = activeCount;
            shop.SetUpdatedAt(now);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(approvedCount);
    }
}

// ─── APPROVE INDIVIDUAL PRODUCT IN GROUP ────────────────────────────────────
public record ApproveGroupProductCommand(Guid BatchId, Guid ProductId) : IRequest<Result>;

public class ApproveGroupProductCommandHandler : IRequestHandler<ApproveGroupProductCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApproveGroupProductCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(ApproveGroupProductCommand request, CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can approve group listings.", "FORBIDDEN");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.ListingBatchId == request.BatchId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure("Product not found in this group.", "PRODUCT_NOT_FOUND");

        var now = _dateTime.UtcNow;
        product.Approve();
        product.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
