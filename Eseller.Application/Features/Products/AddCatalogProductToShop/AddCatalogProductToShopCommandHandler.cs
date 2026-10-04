using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.AddCatalogProductToShop;

public class AddCatalogProductToShopCommandHandler
    : IRequestHandler<AddCatalogProductToShopCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AddCatalogProductToShopCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        AddCatalogProductToShopCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Shopkeeper)
            return Result.Failure<Guid>("Only sellers can request store listings.", "FORBIDDEN");

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == request.ShopId && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return Result.Failure<Guid>("Shop not found.", "SHOP_NOT_FOUND");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
            return Result.Failure<Guid>("You do not have permission for this shop.", "FORBIDDEN");

        if (shopkeeper.Status != ShopkeeperApprovalStatus.Approved)
            return Result.Failure<Guid>(
                "Your store is pending verification. You can list products after Super Admin approves your store.",
                "STORE_NOT_VERIFIED");

        // Banner/cover is optional for listing requests — product goes to Super Admin approval queue.

        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);

        var source = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.Id == request.SourceProductId
                && p.DeletedAt == null
                && p.ShopId == masterShop.Id
                && p.SourceProductId == null
                && p.IsApproved
                && p.IsActive, cancellationToken);

        if (source is null)
            return Result.Failure<Guid>(
                "Catalog product not found or not available for listing.",
                "CATALOG_PRODUCT_NOT_FOUND");

        var alreadyListed = await _context.Products
            .IgnoreQueryFilters()
            .AnyAsync(p =>
                p.ShopId == shop.Id
                && p.SourceProductId == source.Id
                && p.DeletedAt == null
                && p.RejectionReason == null, cancellationToken);

        if (alreadyListed)
            return Result.Failure<Guid>(
                "This product is already in your store or waiting for admin approval.",
                "PRODUCT_ALREADY_REQUESTED");

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

        var slug = await ProductSlug.GenerateUniqueAsync(
            _context, source.Name, prefix: shop.Slug, cancellationToken);
        var now = _dateTime.UtcNow;

        var listing = new Product
        {
            ShopId = shop.Id,
            SourceProductId = source.Id,
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
        }
        catch (DbUpdateException ex) when (ProductSlug.IsUniqueSlugViolation(ex))
        {
            listing.Slug = ProductSlug.WithUniqueSuffix(
                ProductSlug.Normalize($"{shop.Slug}-{source.Name}"));
            listing.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await MasterCatalog.CloneImagesAsync(_context, source.Id, listing.Id, cancellationToken);
        await MasterCatalog.CloneVariantsAsync(_context, source.Id, listing.Id, now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        shop.TotalProducts = usedSlots + 1;
        shop.SetUpdatedAt(now);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(listing.Id);
    }
}
