using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Homepage.GetHomepageSections;

public class GetHomepageSectionsQueryHandler
    : IRequestHandler<GetHomepageSectionsQuery, Result<HomepageSectionsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductBadgeService _badgeService;
    private readonly IDateTime _dateTime;

    public GetHomepageSectionsQueryHandler(
        IApplicationDbContext context,
        IProductBadgeService badgeService,
        IDateTime dateTime)
    {
        _context = context;
        _badgeService = badgeService;
        _dateTime = dateTime;
    }

    public async Task<Result<HomepageSectionsDto>> Handle(
        GetHomepageSectionsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit > 20 ? 20 : (request.Limit < 1 ? 8 : request.Limit);
        var now = _dateTime.UtcNow;

        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);
        var baseQuery = MasterCatalog.StorefrontListings(_context, masterShopId);

        // ? 1. Featured � catalog pins resolve to seller listings (or catalog fallback)
        var featuredIds = await MasterCatalog.ResolveCuratedProductIdsAsync(
            _context, masterShopId, featured: true, hotSelling: false, limit, cancellationToken);

        // ? 2. Best Selling
        var bestSellingIds = await _context.OrderRequestItems
            .Join(_context.OrderRequests,
                oi => oi.OrderRequestId,
                o => o.Id,
                (oi, o) => new { oi, o })
            .Join(_context.ProductVariants,
                x => x.oi.ProductVariantId,
                pv => pv.Id,
                (x, pv) => new { x.oi, x.o, pv })
            .Where(x => x.o.Status == OrderStatus.Delivered)
            .GroupBy(x => x.pv.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(x => x.oi.Quantity) })
            .OrderByDescending(x => x.TotalSold)
            .Take(limit)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        // ? 3. Hot Selling � curated pins first, then last 30 days sales
        var hotSellingIds = await MasterCatalog.ResolveCuratedProductIdsAsync(
            _context, masterShopId, featured: false, hotSelling: true, limit, cancellationToken);

        if (hotSellingIds.Count < limit)
        {
            var hotThreshold = now.AddDays(-30);
            var salesHotIds = await _context.OrderRequestItems
                .Join(_context.OrderRequests,
                    oi => oi.OrderRequestId,
                    o => o.Id,
                    (oi, o) => new { oi, o })
                .Join(_context.ProductVariants,
                    x => x.oi.ProductVariantId,
                    pv => pv.Id,
                    (x, pv) => new { x.oi, x.o, pv })
                .Where(x => x.o.Status == OrderStatus.Delivered
                         && x.o.CreatedAt >= hotThreshold)
                .GroupBy(x => x.pv.ProductId)
                .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(x => x.oi.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Select(x => x.ProductId)
                .ToListAsync(cancellationToken);

            var storefrontSales = await MasterCatalog.MapToStorefrontOrSelfAsync(
                _context, masterShopId, salesHotIds, limit, cancellationToken);

            foreach (var id in storefrontSales)
            {
                if (hotSellingIds.Contains(id)) continue;
                hotSellingIds.Add(id);
                if (hotSellingIds.Count >= limit) break;
            }
        }

        // ? 4. New Arrivals
        var newThreshold = now.AddDays(-30);
        var newArrivalIds = await baseQuery
            .Where(p => p.CreatedAt >= newThreshold)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // ? 5. Flash Sale � map catalog SKUs to storefront listings when needed
        var flashRawIds = await _context.FlashSaleProducts
            .Join(_context.FlashSales,
                fsp => fsp.FlashSaleId,
                fs => fs.Id,
                (fsp, fs) => new { fsp, fs })
            .Where(x => x.fs.IsActive
                     && x.fs.StartDate <= now
                     && x.fs.EndDate >= now)
            .Select(x => x.fsp.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var flashSaleIds = await MasterCatalog.MapToStorefrontOrSelfAsync(
            _context, masterShopId, flashRawIds, limit, cancellationToken);

        // ? 6. Low Stock + Out of Stock (ek query mein)
        var stockData = await baseQuery
            .Select(p => new
            {
                p.Id,
                TotalStock = _context.ProductVariants
                    .Where(v => v.ProductId == p.Id
                             && v.IsActive
                             && v.DeletedAt == null)
                    .Sum(v => (int?)v.StockQty) ?? 0
            })
            .ToListAsync(cancellationToken);

        var lowStockIds = stockData
            .Where(x => x.TotalStock > 0 && x.TotalStock < 10)
            .OrderBy(x => x.TotalStock)
            .Take(limit)
            .Select(x => x.Id)
            .ToList();

        var outOfStockIds = stockData
            .Where(x => x.TotalStock == 0)
            .Take(limit)
            .Select(x => x.Id)
            .ToList();

        var categoryLimit = 11;

        var featuredCategories = await _context.Categories
            .Where(c => c.IsFeaturedOnHomepage && c.IsActive)
            .OrderBy(c => c.HomepageDisplayOrder)
            .Take(categoryLimit)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.ImageUrl,
                c.HomepageDisplayOrder
            })
            .ToListAsync(cancellationToken);

        var categoryIds = featuredCategories.Select(c => c.Id).ToList();

        var categoryProducts = new Dictionary<Guid, List<Guid>>();

        foreach (var categoryId in categoryIds)
        {
            var productIds = await baseQuery
                .Where(p => p.CategoryId == categoryId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)                              // 8 products per category
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            categoryProducts[categoryId] = productIds;
        }

        var allCategoryProductIds = categoryProducts.Values
    .SelectMany(ids => ids)
    .Distinct()
    .ToList();


        // ? Collect all
        var allIds = featuredIds
            .Concat(bestSellingIds)
            .Concat(hotSellingIds)
            .Concat(newArrivalIds)
            .Concat(flashSaleIds)
            .Concat(lowStockIds)
            .Concat(outOfStockIds)
            .Concat(allCategoryProductIds)
            .Distinct()
            .ToList();

        var products = await LoadProductsWithBadges(allIds, cancellationToken);
        var categorySections = featuredCategories.Select(c => new HomepageCategorySectionDto(
    c.Id,
    c.Name,
    c.Slug,
    c.ImageUrl,
    c.HomepageDisplayOrder,
    FilterProducts(products, categoryProducts.GetValueOrDefault(c.Id, new List<Guid>()))
)).ToList();


        var dto = new HomepageSectionsDto(
            Featured: FilterProducts(products, featuredIds),
            BestSelling: FilterProducts(products, bestSellingIds),
            HotSelling: FilterProducts(products, hotSellingIds),
            NewArrivals: FilterProducts(products, newArrivalIds),
            FlashSale: FilterProducts(products, flashSaleIds),
            LowStock: FilterProducts(products, lowStockIds),
            OutOfStock: FilterProducts(products, outOfStockIds),
            Categories: categorySections);

        return Result.Success(dto);
    }

    private async Task<Dictionary<Guid, ProductListDto>> LoadProductsWithBadges(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ProductListDto>();

        // Allow curated catalog fallbacks + storefront listings
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.IsApproved && p.DeletedAt == null)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.SourceProductId,
                PrimaryImageUrl = _context.ProductImages
                    .Where(pi => pi.ProductId == p.Id)
                    .OrderByDescending(pi => pi.IsCover)
                    .ThenBy(pi => pi.SortOrder)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault()
                    ?? (p.SourceProductId == null
                        ? null
                        : _context.ProductImages
                            .Where(pi => pi.ProductId == p.SourceProductId)
                            .OrderByDescending(pi => pi.IsCover)
                            .ThenBy(pi => pi.SortOrder)
                            .Select(pi => pi.ImageUrl)
                            .FirstOrDefault()),
                ShopName = _context.Shops
                    .Where(s => s.Id == p.ShopId).Select(s => s.Name)
                    .FirstOrDefault() ?? string.Empty,
                ShopSlug = _context.Shops
                    .Where(s => s.Id == p.ShopId).Select(s => s.Slug)
                    .FirstOrDefault() ?? string.Empty,
                ShopRating = _context.Shops
                    .Where(s => s.Id == p.ShopId).Select(s => s.Rating)
                    .FirstOrDefault(),
                ShopBadgeText = (
                    from s in _context.Shops
                    where s.Id == p.ShopId
                    join sc in _context.ShopCategories on s.ShopCategoryId equals sc.Id into scj
                    from sc in scj.DefaultIfEmpty()
                    select s.CustomBadgeText ?? sc.BadgeText ?? sc.Name ?? "Bronze"
                ).FirstOrDefault(),
                ShopBadgeColor = (
                    from s in _context.Shops
                    where s.Id == p.ShopId
                    join sc in _context.ShopCategories on s.ShopCategoryId equals sc.Id into scj
                    from sc in scj.DefaultIfEmpty()
                    select sc.BadgeColor ?? "#94A3B8"
                ).FirstOrDefault(),
                CategoryName = _context.Categories
                    .Where(c => c.Id == p.CategoryId).Select(c => c.Name)
                    .FirstOrDefault() ?? string.Empty,
                BrandName = p.BrandId == null
                    ? null
                    : _context.Brands
                        .Where(b => b.Id == p.BrandId).Select(b => b.Name)
                        .FirstOrDefault(),
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.RejectionReason,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var badges = await _badgeService.CalculateBadgesAsync(productIds, cancellationToken);

        return products.ToDictionary(
            p => p.Id,
            p => new ProductListDto(
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.PrimaryImageUrl,
                p.ShopName,
                p.ShopSlug,
                p.CategoryName,
                p.BrandName,
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.IsApproved ? "Approved" : (p.RejectionReason == null ? "Pending" : "Rejected"),
                p.RejectionReason,
                p.CreatedAt,
                badges.GetValueOrDefault(p.Id),
                ShopRating: p.ShopRating,
                ShopBadgeText: p.ShopBadgeText,
                ShopBadgeColor: p.ShopBadgeColor));
    }

    private static List<ProductListDto> FilterProducts(
        Dictionary<Guid, ProductListDto> products,
        List<Guid> orderedIds)
    {
        return orderedIds
            .Where(id => products.ContainsKey(id))
            .Select(id => products[id])
            .ToList();
    }
}