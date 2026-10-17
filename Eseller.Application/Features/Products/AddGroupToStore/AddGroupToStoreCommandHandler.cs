using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.AddGroupToStore;

public class AddGroupToStoreCommandHandler
    : IRequestHandler<AddGroupToStoreCommand, Result<AddGroupToStoreResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public AddGroupToStoreCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notifications = notifications;
    }

    public async Task<Result<AddGroupToStoreResultDto>> Handle(
        AddGroupToStoreCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<AddGroupToStoreResultDto>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.Shopkeeper && role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<AddGroupToStoreResultDto>("Only sellers or admins can request group store listings.", "FORBIDDEN");

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == request.ShopId && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return Result.Failure<AddGroupToStoreResultDto>("Shop not found.", "SHOP_NOT_FOUND");

        if (role == RoleType.Shopkeeper)
        {
            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<AddGroupToStoreResultDto>("You do not have permission for this shop.", "FORBIDDEN");

            if (shopkeeper.Status != ShopkeeperApprovalStatus.Approved)
                return Result.Failure<AddGroupToStoreResultDto>(
                    "Your store is pending verification. You can list products after Super Admin approves your store.",
                    "STORE_NOT_VERIFIED");
        }

        var limit = await MasterCatalog.ResolveProductLimitAsync(_context, shop, cancellationToken);
        var usedSlots = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p =>
                p.ShopId == shop.Id
                && p.DeletedAt == null
                && p.RejectionReason == null, cancellationToken);

        var availableSlots = limit - usedSlots;
        if (availableSlots <= 0)
            return Result.Failure<AddGroupToStoreResultDto>(
                $"Package capacity reached. Your current plan allows a maximum of {limit} products ({usedSlots} currently listed). Please upgrade your package.",
                "PRODUCT_LIMIT_REACHED");

        var requestedCount = Math.Max(1, Math.Min(request.Count, 200));
        var targetCount = Math.Min(requestedCount, availableSlots);

        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);

        var existingListedIds = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ShopId == shop.Id && p.DeletedAt == null && p.RejectionReason == null)
            .Select(p => p.SourceProductId ?? p.Id)
            .ToListAsync(cancellationToken);
        var existingSet = existingListedIds.ToHashSet();

        var query = _context.Products
            .IgnoreQueryFilters()
            .Where(p => (p.ShopId == masterShop.Id || p.SourceProductId == null)
                        && p.ShopId != shop.Id
                        && p.SourceProductId == null
                        && p.IsActive
                        && p.DeletedAt == null
                        && !existingSet.Contains(p.Id));

        if (request.CategoryIds != null && request.CategoryIds.Count > 0)
        {
            query = query.Where(p => request.CategoryIds.Contains(p.CategoryId));
        }

        if (request.BrandId.HasValue && request.BrandId.Value != Guid.Empty)
        {
            query = query.Where(p => p.BrandId == request.BrandId.Value);
        }

        if (request.MinPrice.HasValue && request.MinPrice.Value > 0)
        {
            query = query.Where(p => p.BasePrice >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue && request.MaxPrice.Value > 0)
        {
            query = query.Where(p => p.BasePrice <= request.MaxPrice.Value);
        }

        var candidateIds = await query.Select(p => p.Id).ToListAsync(cancellationToken);
        if (candidateIds.Count == 0)
        {
            // If all matching products are already in shop, allow broader candidates so seller can fulfill quota
            candidateIds = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => (p.ShopId == masterShop.Id || p.SourceProductId == null)
                            && p.ShopId != shop.Id
                            && p.SourceProductId == null
                            && p.IsActive
                            && p.DeletedAt == null)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
        }

        if (candidateIds.Count == 0)
        {
            return Result.Failure<AddGroupToStoreResultDto>(
                "No available warehouse products match your selected criteria.",
                "NO_MATCHING_PRODUCTS");
        }

        var rand = new Random();
        var selectedSourceIds = new List<Guid>();

        if (candidateIds.Count >= targetCount)
        {
            selectedSourceIds = candidateIds
                .OrderBy(_ => rand.Next())
                .Take(targetCount)
                .ToList();
        }
        else
        {
            // If candidate catalog products are fewer than requested, cycle candidates to deliver exact targetCount
            selectedSourceIds.AddRange(candidateIds.OrderBy(_ => rand.Next()));
            while (selectedSourceIds.Count < targetCount && candidateIds.Count > 0)
            {
                var needed = targetCount - selectedSourceIds.Count;
                var refill = candidateIds.OrderBy(_ => rand.Next()).Take(needed).ToList();
                selectedSourceIds.AddRange(refill);
            }
        }

        var distinctSourceIds = selectedSourceIds.Distinct().ToList();
        var sourceProductsDict = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => distinctSourceIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var sourceProducts = selectedSourceIds
            .Select(id => sourceProductsDict.TryGetValue(id, out var sp) ? sp : null)
            .Where(sp => sp is not null)
            .Select(sp => sp!)
            .ToList();

        var batchId = Guid.NewGuid();
        var now = _dateTime.UtcNow;
        var batchName = !string.IsNullOrWhiteSpace(request.CustomBatchName)
            ? request.CustomBatchName.Trim()
            : $"Group #{now:yyMMdd}-{rand.Next(1000, 9999)}";

        // Category filter summary
        string? categorySummary = null;
        if (request.CategoryIds != null && request.CategoryIds.Count > 0)
        {
            var catNames = await _context.Categories
                .Where(c => request.CategoryIds.Contains(c.Id))
                .Select(c => c.Name)
                .ToListAsync(cancellationToken);
            categorySummary = string.Join(", ", catNames);
        }

        string? brandSummary = null;
        if (request.BrandId.HasValue && request.BrandId.Value != Guid.Empty)
        {
            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Id == request.BrandId.Value, cancellationToken);
            brandSummary = brand?.Name;
        }

        var groupListing = new ProductGroupListing
        {
            Id = batchId,
            ShopId = shop.Id,
            BatchName = batchName,
            RequestedCount = request.Count,
            DeliveredCount = sourceProducts.Count,
            CategoryFilter = categorySummary,
            BrandFilter = brandSummary,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice
        };
        groupListing.SetCreatedAt(now);
        groupListing.SetUpdatedAt(now);
        _context.ProductGroupListings.Add(groupListing);

        // Batch clone products
        int successfullyAdded = 0;
        foreach (var source in sourceProducts)
        {
            var slug = await ProductSlug.GenerateUniqueAsync(
                _context, source.Name, prefix: shop.Slug, cancellationToken);

            var listing = new Product
            {
                ShopId = shop.Id,
                SourceProductId = source.Id,
                ListingBatchId = batchId,
                BatchName = batchName,
                CategoryId = source.CategoryId,
                BrandId = source.BrandId,
                Name = source.Name,
                Slug = slug,
                Description = source.Description,
                BasePrice = source.BasePrice,
                IsFeatured = false,
                IsApproved = false,
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
                successfullyAdded++;
            }
            catch (Exception)
            {
                // Continue with next
            }
        }

        shop.TotalProducts = usedSlots + successfullyAdded;
        groupListing.DeliveredCount = successfullyAdded;
        shop.SetUpdatedAt(now);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify
        await _notifications.NotifyAdminsAsync(
            NotificationType.ProductListingRequest,
            "Group Products Added",
            $"{shop.Name} imported {successfullyAdded} warehouse products under \"{batchName}\".",
            NotificationEntityType.Product,
            batchId,
            "/products/groups",
            $"GroupListingRequest:{batchId:N}",
            cancellationToken);

        var accountId = _currentUserService.AccountId.Value;
        await _notifications.NotifyAsync(
            accountId,
            RoleType.Shopkeeper,
            NotificationType.NewProduct,
            "Group Import Complete",
            $"{successfullyAdded} products added to your shop under \"{batchName}\" and are pending approval.",
            NotificationEntityType.Product,
            batchId,
            "/products",
            $"GroupListingAdded:{batchId:N}:Seller:{accountId:N}",
            cancellationToken);

        return Result.Success(new AddGroupToStoreResultDto(
            batchId,
            batchName,
            successfullyAdded,
            request.Count,
            $"Successfully imported {successfullyAdded} warehouse products into {shop.Name}."
        ));
    }
}
