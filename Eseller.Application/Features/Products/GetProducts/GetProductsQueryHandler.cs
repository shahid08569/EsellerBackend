using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetProducts;

public class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, Result<PagedList<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductBadgeService _badgeService;

    public GetProductsQueryHandler(
        IApplicationDbContext context,
        IProductBadgeService badgeService)
    {
        _context = context;
        _badgeService = badgeService;
    }

    public async Task<Result<PagedList<ProductListDto>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        // Featured homepage slider: resolve catalog pins → listings (or catalog fallback)
        if (request.IsFeatured == true
            && !request.CategoryId.HasValue
            && !request.BrandId.HasValue
            && !request.ShopId.HasValue
            && string.IsNullOrWhiteSpace(request.Search))
        {
            var curatedIds = await MasterCatalog.ResolveCuratedProductIdsAsync(
                _context,
                masterShopId,
                featured: true,
                hotSelling: false,
                limit: Math.Max(request.PageSize, 8),
                cancellationToken);

            var curatedProducts = await _context.Products
                .Where(p => curatedIds.Contains(p.Id) && p.IsActive && p.IsApproved && p.DeletedAt == null)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.BasePrice,
                    p.CategoryId,
                    p.BrandId,
                    p.SourceProductId,
                    p.ShopId,
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
                    ShopName = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                    ShopSlug = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Slug).FirstOrDefault() ?? string.Empty,
                    ShopRating = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Rating).FirstOrDefault(),
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
                    CategoryName = _context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                    BrandName = p.BrandId == null
                        ? null
                        : _context.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                    p.AvgRating,
                    p.ReviewCount,
                    p.IsFeatured,
                    p.IsApproved,
                    p.RejectionReason,
                    p.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var ordered = curatedIds
                .Select(id => curatedProducts.FirstOrDefault(p => p.Id == id))
                .Where(p => p is not null)
                .ToList();

            var curatedBadges = await _badgeService.CalculateBadgesAsync(
                ordered.Select(p => p!.Id).ToList(), cancellationToken);

            var curatedDtos = ordered.Select(p => new ProductListDto(
                p!.Id,
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
                "Approved",
                p.RejectionReason,
                p.CreatedAt,
                curatedBadges.GetValueOrDefault(p.Id),
                p.CategoryId,
                p.BrandId,
                p.SourceProductId,
                p.ShopId,
                p.ShopRating,
                p.ShopBadgeText,
                p.ShopBadgeColor,
                ReviewCount: p.ReviewCount)).ToList();

            return Result.Success(new PagedList<ProductListDto>(
                curatedDtos,
                curatedDtos.Count,
                request.PageNumber,
                request.PageSize));
        }

        // Global marketplace vs single-shop page (seller-own products only on shop page)
        var query = request.ShopId.HasValue
            ? MasterCatalog.ShopListings(_context, request.ShopId.Value, masterShopId)
            : MasterCatalog.StorefrontListings(_context, masterShopId);

        // Filters
        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        if (request.BrandId.HasValue)
            query = query.Where(p => p.BrandId == request.BrandId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(p => p.BasePrice >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(p => p.BasePrice <= request.MaxPrice.Value);

        if (request.MinRating.HasValue)
            query = query.Where(p => p.AvgRating >= request.MinRating.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(search)
                                  || (p.Description != null && p.Description.ToLower().Contains(search)));
        }

        if (request.IsFeatured.HasValue)
            query = query.Where(p => p.IsFeatured == request.IsFeatured.Value);

        query = request.SortBy.ToLowerInvariant() switch
        {
            "popular" => query
                .Where(p => p.ViewCount > 0)
                .OrderByDescending(p => p.ViewCount),

            "price_asc" => query.OrderBy(p => p.BasePrice),
            "price_desc" => query.OrderByDescending(p => p.BasePrice),
            "rating" => query.OrderByDescending(p => p.AvgRating),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };
        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.CategoryId,
                p.BrandId,
                p.SourceProductId,
                p.ShopId,
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
                ShopName = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                ShopSlug = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Slug).FirstOrDefault() ?? string.Empty,
                ShopRating = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Rating).FirstOrDefault(),
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
                CategoryName = _context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                BrandName = p.BrandId == null
                    ? null
                    : _context.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                p.AvgRating,
                p.ReviewCount,
                p.IsFeatured,
                p.IsApproved,
                p.RejectionReason,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();

        // ✅ Calculate badges
        var badges = await _badgeService.CalculateBadgesAsync(productIds, cancellationToken);

        // ✅ Build DTOs with badges
        var productDtos = products.Select(p => new ProductListDto(
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
            p.IsApproved
                ? "Approved"
                : (p.RejectionReason == null ? "Pending" : "Rejected"),
            p.RejectionReason,
            p.CreatedAt,
            badges.GetValueOrDefault(p.Id),
            p.CategoryId,
            p.BrandId,
            p.SourceProductId,
            p.ShopId,
            p.ShopRating,
            p.ShopBadgeText,
            p.ShopBadgeColor,
            ReviewCount: p.ReviewCount)).ToList();

        var pagedList = new PagedList<ProductListDto>(
            productDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}