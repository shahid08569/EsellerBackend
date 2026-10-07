using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.CreateProduct;

public class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public CreateProductCommandHandler(
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

    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isSeller = role == RoleType.Shopkeeper;

        if (!isAdmin && !isSeller)
            return Result.Failure<Guid>(
                "Only Super Admin or sellers can create products.",
                "FORBIDDEN");

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
            return Result.Failure<Guid>(
                "Category not found. Please select a valid category.",
                "CATEGORY_NOT_FOUND");

        if (request.BrandId.HasValue)
        {
            var brandExists = await _context.Brands
                .AnyAsync(b => b.Id == request.BrandId.Value && b.IsActive, cancellationToken);

            if (!brandExists)
                return Result.Failure<Guid>("Brand not found.", "BRAND_NOT_FOUND");
        }

        Shop targetShop;
        bool isPendingApproval;

        if (isAdmin)
        {
            // SuperAdmin / Partner → Platform Master Warehouse (approved, not live until opted-in)
            try
            {
                targetShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result.Failure<Guid>(
                    $"Master warehouse is not ready: {ex.Message}",
                    "MASTER_WAREHOUSE_ERROR");
            }

            isPendingApproval = false;
        }
        else
        {
            // Seller → own shop listing, pending SuperAdmin approval
            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value, cancellationToken);

            if (shopkeeper is null)
                return Result.Failure<Guid>("Shopkeeper profile not found.", "SHOPKEEPER_NOT_FOUND");

            if (shopkeeper.Status != ShopkeeperApprovalStatus.Approved)
                return Result.Failure<Guid>(
                    "Your store must be verified by Super Admin before you can add products.",
                    "SHOP_NOT_VERIFIED");

            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null, cancellationToken);

            if (shop is null)
                return Result.Failure<Guid>("Store not found.", "SHOP_NOT_FOUND");

            if (!shop.IsActive)
                return Result.Failure<Guid>(
                    "Your store is inactive. Contact Super Admin.",
                    "SHOP_INACTIVE");

            var limit = await MasterCatalog.ResolveProductLimitAsync(_context, shop, cancellationToken);
            var usedSlots = await _context.Products
                .IgnoreQueryFilters()
                .CountAsync(p =>
                    p.ShopId == shop.Id
                    && p.DeletedAt == null
                    && p.RejectionReason == null, cancellationToken);

            if (usedSlots >= limit)
                return Result.Failure<Guid>(
                    $"Package capacity reached. Your current plan allows a maximum of {limit} products. Please upgrade your package.",
                    "PRODUCT_LIMIT_REACHED");

            targetShop = shop;
            isPendingApproval = true;
        }

        var slugPrefix = isAdmin ? null : targetShop.Slug;
        var slug = await ProductSlug.GenerateUniqueAsync(
            _context, request.Name, prefix: slugPrefix, cancellationToken);
        var now = _dateTime.UtcNow;

        var product = new Product
        {
            ShopId = targetShop.Id,
            SourceProductId = null,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description,
            BasePrice = request.BasePrice,
            IsFeatured = false,
            IsApproved = !isPendingApproval,
            IsStorefrontLive = false,
            ViewCount = 0,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            IsActive = true
        };
        product.SetCreatedAt(now);
        product.SetUpdatedAt(now);

        _context.Products.Add(product);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ProductSlug.IsUniqueSlugViolation(ex))
        {
            product.Slug = ProductSlug.WithUniqueSuffix(
                ProductSlug.Normalize(isAdmin ? request.Name : $"{targetShop.Slug}-{request.Name}"));
            product.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        targetShop.TotalProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p =>
                p.ShopId == targetShop.Id
                && p.DeletedAt == null, cancellationToken);
        targetShop.SetUpdatedAt(now);
        await _context.SaveChangesAsync(cancellationToken);

        if (isPendingApproval)
        {
            await _notifications.NotifyAdminsAsync(
                NotificationType.ProductListingRequest,
                "Seller Product Submitted",
                $"{targetShop.Name} submitted \"{product.Name}\" for approval.",
                NotificationEntityType.Product,
                product.Id,
                "/products?status=pending",
                $"ProductListingRequest:{product.Id:N}",
                cancellationToken);

            await _notifications.NotifyAsync(
                _currentUserService.AccountId.Value,
                RoleType.Shopkeeper,
                NotificationType.NewProduct,
                "Product Submitted",
                $"\"{product.Name}\" was submitted and is pending Super Admin approval.",
                NotificationEntityType.Product,
                product.Id,
                "/products",
                $"NewProduct:{product.Id:N}:Seller:{_currentUserService.AccountId.Value:N}",
                cancellationToken);
        }

        return Result.Success(product.Id);
    }
}
