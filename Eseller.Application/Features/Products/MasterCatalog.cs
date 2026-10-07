using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products;

public static class MasterCatalog
{
    public const string ShopSlug = "master-warehouse";
    public const string ShopName = "Platform Master Warehouse";
    public const int DefaultFreeProductLimit = 200;
    private const string SellerListingDescriptionPrefix = "Warehouse listing for";

    public static async Task<Shop> GetOrCreateMasterShopAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        // Include soft-deleted rows: IX_Shops_Slug is unique across ALL rows,
        // so a deleted master-warehouse must be restored — never inserted again.
        var shop = await context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Slug == ShopSlug, cancellationToken);

        if (shop is null)
        {
            shop = await context.Shops
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Name == ShopName, cancellationToken);
        }

        if (shop is not null)
        {
            if (EnsureMasterShopFields(shop))
                await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Re-check under IgnoreQueryFilters in case of race / stale tracking
            shop = await context.Shops
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Slug == ShopSlug, cancellationToken);

            if (shop is not null)
            {
                // Attach tracked entity for possible field repair
                shop = await context.Shops
                    .IgnoreQueryFilters()
                    .FirstAsync(s => s.Id == shop.Id, cancellationToken);
                if (EnsureMasterShopFields(shop))
                    await context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var shopkeeperId = await ResolvePlatformShopkeeperIdAsync(context, cancellationToken);
                if (shopkeeperId is null)
                    throw new InvalidOperationException(
                        "Cannot create master catalog shop: no Super Admin account found to own it. Seed Super Admin first.");

                var now = DateTime.UtcNow;
                shop = new Shop
                {
                    ShopkeeperId = shopkeeperId.Value,
                    Name = ShopName,
                    Slug = ShopSlug,
                    Description = "Super Admin master product catalog",
                    IsActive = true
                };
                shop.SetCreatedAt(now);
                shop.SetUpdatedAt(now);
                context.Shops.Add(shop);

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Concurrent create race / soft-deleted slug already exists — reuse existing row.
                    if (context is DbContext db)
                        db.Entry(shop).State = EntityState.Detached;

                    shop = await context.Shops
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(s => s.Slug == ShopSlug, cancellationToken)
                        ?? throw new InvalidOperationException(
                            "Master warehouse slug conflict could not be resolved. Check dbo.Shops for slug 'master-warehouse'.");

                    if (EnsureMasterShopFields(shop))
                        await context.SaveChangesAsync(cancellationToken);
                }
            }
        }

        await MigrateLegacyCatalogProductsAsync(context, shop, cancellationToken);
        await RepairSellerListingLinksAndImagesAsync(context, shop.Id, cancellationToken);

        return shop;
    }

    /// <returns>True if any field was changed and SaveChanges is needed.</returns>
    private static bool EnsureMasterShopFields(Shop shop)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(shop.Slug) || shop.Slug != ShopSlug)
        {
            shop.Slug = ShopSlug;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(shop.Name) || shop.Name != ShopName)
        {
            shop.Name = ShopName;
            changed = true;
        }

        if (!shop.IsActive)
        {
            shop.IsActive = true;
            changed = true;
        }

        if (shop.DeletedAt is not null)
        {
            shop.DeletedAt = null;
            changed = true;
        }

        if (changed)
            shop.SetUpdatedAt(DateTime.UtcNow);

        return changed;
    }

    /// <summary>
    /// Move legacy admin catalog products (often stuck on deleted/inactive shops) onto master warehouse.
    /// Seller "Add to Store" clones (description starts with Warehouse listing for...) stay on seller shops.
    /// </summary>
    private static async Task MigrateLegacyCatalogProductsAsync(
        IApplicationDbContext context,
        Shop masterShop,
        CancellationToken cancellationToken)
    {
        var candidates = await context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                p.DeletedAt == null
                && p.SourceProductId == null
                && p.ShopId != masterShop.Id
                && (p.Description == null
                    || !p.Description.StartsWith(SellerListingDescriptionPrefix)))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var product in candidates)
        {
            product.ShopId = masterShop.Id;
            product.IsApproved = true;
            product.IsActive = true;
            product.SetUpdatedAt(now);
        }

        masterShop.TotalProducts = await context.Products
            .IgnoreQueryFilters()
            .CountAsync(p =>
                p.ShopId == masterShop.Id
                && p.DeletedAt == null
                && p.SourceProductId == null, cancellationToken);

        masterShop.SetUpdatedAt(now);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Link old seller listings to catalog by name and copy missing images/variants from source.
    /// </summary>
    private static async Task RepairSellerListingLinksAndImagesAsync(
        IApplicationDbContext context,
        Guid masterShopId,
        CancellationToken cancellationToken)
    {
        var catalogProducts = await context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                p.DeletedAt == null
                && p.ShopId == masterShopId
                && p.SourceProductId == null
                && p.IsActive)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        if (catalogProducts.Count == 0)
            return;

        var catalogByName = catalogProducts
            .GroupBy(p => p.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        var orphanListings = await context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                p.DeletedAt == null
                && p.ShopId != masterShopId
                && (p.SourceProductId == null
                    || (p.Description != null && p.Description.StartsWith(SellerListingDescriptionPrefix))))
            .ToListAsync(cancellationToken);

        var changed = false;
        var now = DateTime.UtcNow;

        foreach (var listing in orphanListings)
        {
            var key = listing.Name.Trim().ToLowerInvariant();
            if (!catalogByName.TryGetValue(key, out var sourceId))
                continue;

            if (listing.Id == sourceId)
                continue;

            if (listing.SourceProductId != sourceId)
            {
                listing.SourceProductId = sourceId;
                listing.SetUpdatedAt(now);
                changed = true;
            }

            var hasImages = await context.ProductImages
                .AnyAsync(pi => pi.ProductId == listing.Id, cancellationToken);

            if (!hasImages)
            {
                await CloneImagesAsync(context, sourceId, listing.Id, cancellationToken);
                changed = true;
            }

            var hasVariants = await context.ProductVariants
                .IgnoreQueryFilters()
                .AnyAsync(v => v.ProductId == listing.Id && v.DeletedAt == null, cancellationToken);

            if (!hasVariants)
            {
                await CloneVariantsAsync(context, sourceId, listing.Id, now, cancellationToken);
                changed = true;
            }
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task CloneImagesAsync(
        IApplicationDbContext context,
        Guid sourceProductId,
        Guid targetProductId,
        CancellationToken cancellationToken)
    {
        var images = await context.ProductImages
            .Where(pi => pi.ProductId == sourceProductId)
            .OrderByDescending(pi => pi.IsCover)
            .ThenBy(pi => pi.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var img in images)
        {
            context.ProductImages.Add(new ProductImage
            {
                ProductId = targetProductId,
                ImageUrl = img.ImageUrl,
                SortOrder = img.SortOrder,
                IsCover = img.IsCover
            });
        }
    }

    public static async Task CloneVariantsAsync(
        IApplicationDbContext context,
        Guid sourceProductId,
        Guid targetProductId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var variants = await context.ProductVariants
            .IgnoreQueryFilters()
            .Where(v => v.ProductId == sourceProductId && v.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var variantIds = variants.Select(v => v.Id).ToList();
        var attributes = await context.VariantAttributeValues
            .Where(a => variantIds.Contains(a.ProductVariantId))
            .ToListAsync(cancellationToken);

        var attrsByVariant = attributes
            .GroupBy(a => a.ProductVariantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var variant in variants)
        {
            var clone = new ProductVariant
            {
                ProductId = targetProductId,
                ImageUrl = variant.ImageUrl,
                StockQty = variant.StockQty,
                LowStockThreshold = variant.LowStockThreshold,
                Price = variant.Price,
                Sku = $"{variant.Sku}-{targetProductId.ToString("N")[..8]}",
                IsActive = variant.IsActive
            };
            clone.SetCreatedAt(utcNow);
            clone.SetUpdatedAt(utcNow);
            context.ProductVariants.Add(clone);
            await context.SaveChangesAsync(cancellationToken);

            if (attrsByVariant.TryGetValue(variant.Id, out var attrs))
            {
                foreach (var attr in attrs)
                {
                    context.VariantAttributeValues.Add(new VariantAttributeValue
                    {
                        ProductVariantId = clone.Id,
                        AttributeName = attr.AttributeName,
                        AttributeValue = attr.AttributeValue
                    });
                }
            }
        }
    }

    private static async Task<Guid?> ResolvePlatformShopkeeperIdAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var superAdminAccountIds = await context.Accounts
            .AsNoTracking()
            .Where(a => a.RoleType == RoleType.SuperAdmin)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (superAdminAccountIds.Count == 0)
            return null;

        var existing = await context.Shopkeepers
            .FirstOrDefaultAsync(sk => superAdminAccountIds.Contains(sk.AccountId), cancellationToken);

        if (existing is not null)
        {
            if (existing.Status != ShopkeeperApprovalStatus.Approved)
            {
                existing.Status = ShopkeeperApprovalStatus.Approved;
                existing.ApprovedAt ??= DateTime.UtcNow;
                existing.SetUpdatedAt(DateTime.UtcNow);
                await context.SaveChangesAsync(cancellationToken);
            }

            return existing.Id;
        }

        var accountId = superAdminAccountIds[0];
        var now = DateTime.UtcNow;
        var shopkeeper = new Shopkeeper
        {
            AccountId = accountId,
            Name = "Platform Catalog Owner",
            Phone = "0000000000",
            Status = ShopkeeperApprovalStatus.Approved,
            ApprovedAt = now,
            ApprovedByAccountId = accountId
        };
        shopkeeper.SetCreatedAt(now);
        shopkeeper.SetUpdatedAt(now);

        context.Shopkeepers.Add(shopkeeper);
        await context.SaveChangesAsync(cancellationToken);
        return shopkeeper.Id;
    }

    public static IQueryable<Product> CatalogProducts(IApplicationDbContext context, Guid masterShopId) =>
        context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                p.DeletedAt == null
                && p.ShopId == masterShopId
                && p.SourceProductId == null);

    /// <summary>
    /// Customer storefront: approved seller listings only — never master warehouse catalog.
    /// </summary>
    public static async Task<Guid?> TryGetMasterShopIdAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Shops
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Slug == ShopSlug || s.Name == ShopName)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Global customer marketplace:
    /// - Master Warehouse SKUs SuperAdmin marked live
    /// - Seller listings cloned from warehouse (SourceProductId set)
    /// Seller-created originals (SourceProductId null on a seller shop) are NOT global —
    /// they stay on that seller only (see ShopListings).
    /// </summary>
    public static IQueryable<Product> StorefrontListings(
        IApplicationDbContext context,
        Guid? masterShopId) =>
        context.Products
            .Where(p =>
                p.IsActive
                && p.IsApproved
                && p.DeletedAt == null
                && (
                    // Warehouse clones listed by sellers → visible globally under that seller
                    p.SourceProductId != null
                    // Master warehouse live SKUs
                    || (masterShopId.HasValue
                        && p.ShopId == masterShopId.Value
                        && p.SourceProductId == null
                        && p.IsStorefrontLive)
                ));

    /// <summary>
    /// Products visible on a specific seller shop page: warehouse clones + seller-own approved.
    /// </summary>
    public static IQueryable<Product> ShopListings(
        IApplicationDbContext context,
        Guid shopId,
        Guid? masterShopId) =>
        context.Products
            .Where(p =>
                p.ShopId == shopId
                && p.IsActive
                && p.IsApproved
                && p.DeletedAt == null
                && (
                    p.SourceProductId != null
                    || !masterShopId.HasValue
                    || p.ShopId != masterShopId.Value
                ));

    /// <summary>
    /// Homepage curated IDs for Featured / Hot Selling.
    /// Prefers seller storefront listings linked to flagged catalog SKUs;
    /// falls back to the catalog product itself so admin merchandising works.
    /// </summary>
    public static async Task<List<Guid>> ResolveCuratedProductIdsAsync(
        IApplicationDbContext context,
        Guid? masterShopId,
        bool featured,
        bool hotSelling,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit < 1) return new List<Guid>();

        var catalogQuery = context.Products
            .Where(p =>
                p.IsActive
                && p.IsApproved
                && p.DeletedAt == null
                && p.SourceProductId == null
                && (!masterShopId.HasValue || p.ShopId == masterShopId.Value));

        if (featured) catalogQuery = catalogQuery.Where(p => p.IsFeatured);
        if (hotSelling) catalogQuery = catalogQuery.Where(p => p.IsHotSelling);

        var catalogIds = await catalogQuery
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => p.Id)
            .Take(Math.Max(limit * 4, limit))
            .ToListAsync(cancellationToken);

        var listingRows = catalogIds.Count == 0
            ? new List<(Guid Id, Guid SourceProductId, DateTime CreatedAt)>()
            : (await StorefrontListings(context, masterShopId)
                .Where(p => p.SourceProductId != null && catalogIds.Contains(p.SourceProductId.Value))
                .Select(p => new { p.Id, SourceProductId = p.SourceProductId!.Value, p.CreatedAt })
                .ToListAsync(cancellationToken))
              .Select(x => (x.Id, x.SourceProductId, x.CreatedAt))
              .ToList();

        // Which catalog SKUs are opted-in to the customer website (no seller listing needed)
        var liveCatalogIds = catalogIds.Count == 0
            ? new HashSet<Guid>()
            : (await context.Products
                .Where(p => catalogIds.Contains(p.Id) && p.IsStorefrontLive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken))
              .ToHashSet();

        var result = new List<Guid>();
        var usedSources = new HashSet<Guid>();

        foreach (var catalogId in catalogIds)
        {
            var listing = listingRows
                .Where(l => l.SourceProductId == catalogId)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefault();

            // Prefer seller listing; else master product only if SuperAdmin marked it live
            if (listing.Id != Guid.Empty)
            {
                result.Add(listing.Id);
                usedSources.Add(catalogId);
            }
            else if (liveCatalogIds.Contains(catalogId))
            {
                result.Add(catalogId);
                usedSources.Add(catalogId);
            }

            if (result.Count >= limit) break;
        }

        if (result.Count < limit)
        {
            var directQuery = StorefrontListings(context, masterShopId);
            if (featured) directQuery = directQuery.Where(p => p.IsFeatured);
            if (hotSelling) directQuery = directQuery.Where(p => p.IsHotSelling);

            var extras = await directQuery
                .Where(p => !result.Contains(p.Id))
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => p.Id)
                .Take(limit - result.Count)
                .ToListAsync(cancellationToken);

            result.AddRange(extras);
        }

        return result;
    }

    /// <summary>
    /// Map flash-sale / curated catalog product IDs to preferred storefront listing IDs.
    /// </summary>
    public static async Task<List<Guid>> MapToStorefrontOrSelfAsync(
        IApplicationDbContext context,
        Guid? masterShopId,
        IReadOnlyList<Guid> productIds,
        int limit,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return new List<Guid>();

        var products = await context.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.IsApproved && p.DeletedAt == null)
            .Select(p => new { p.Id, p.ShopId, p.SourceProductId, p.IsStorefrontLive })
            .ToListAsync(cancellationToken);

        var catalogIds = products
            .Where(p => p.SourceProductId == null && (!masterShopId.HasValue || p.ShopId == masterShopId.Value))
            .Select(p => p.Id)
            .ToList();

        var listings = catalogIds.Count == 0
            ? new List<(Guid Id, Guid SourceId, DateTime CreatedAt)>()
            : (await StorefrontListings(context, masterShopId)
                .Where(p => p.SourceProductId != null && catalogIds.Contains(p.SourceProductId.Value))
                .Select(p => new { p.Id, SourceId = p.SourceProductId!.Value, p.CreatedAt })
                .ToListAsync(cancellationToken))
              .Select(x => (x.Id, x.SourceId, x.CreatedAt))
              .ToList();

        var mapped = new List<Guid>();
        foreach (var id in productIds)
        {
            var row = products.FirstOrDefault(p => p.Id == id);
            if (row is null) continue;

            if (row.SourceProductId == null && (!masterShopId.HasValue || row.ShopId == masterShopId))
            {
                // Catalog SKU → prefer a live seller listing; else master only if opted-in
                var listing = listings
                    .Where(l => l.SourceId == id)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => l.Id)
                    .FirstOrDefault();
                if (listing != Guid.Empty)
                    mapped.Add(listing);
                else if (row.IsStorefrontLive)
                    mapped.Add(id);
            }
            else if (!masterShopId.HasValue || row.ShopId != masterShopId)
            {
                mapped.Add(id);
            }

            if (mapped.Count >= limit) break;
        }

        return mapped.Distinct().Take(limit).ToList();
    }

    public static async Task<int> ResolveProductLimitAsync(
        IApplicationDbContext context,
        Shop shop,
        CancellationToken cancellationToken)
    {
        if (!shop.ShopCategoryId.HasValue)
            return DefaultFreeProductLimit;

        var tier = await context.ShopCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == shop.ShopCategoryId.Value && c.DeletedAt == null, cancellationToken);

        if (tier is null || tier.MaxProductListings <= 0)
            return DefaultFreeProductLimit;

        return tier.MaxProductListings;
    }
}
