using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Search.SearchProducts;

public class SearchProductsQueryHandler
    : IRequestHandler<SearchProductsQuery, Result<PagedList<SearchResultDto>>>
{
    private readonly IApplicationDbContext _context;

    public SearchProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<SearchResultDto>>> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken)
    {
        // Minimum 2 characters for search
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Trim().Length < 2)
            return Result.Failure<PagedList<SearchResultDto>>(
                "Search query must be at least 2 characters.",
                "INVALID_QUERY");

        var searchTerm = request.Query.Trim().ToLower();

        // Base query — only approved + active products
        var query = _context.Products
            .Where(p => p.IsActive
                     && p.IsApproved
                     && p.DeletedAt == null
                     && (p.Name.ToLower().Contains(searchTerm)
                         || (p.Description != null && p.Description.ToLower().Contains(searchTerm))));

        // Filters
        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        if (request.BrandId.HasValue)
            query = query.Where(p => p.BrandId == request.BrandId.Value);

        if (request.ShopId.HasValue)
            query = query.Where(p => p.ShopId == request.ShopId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(p => p.BasePrice >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(p => p.BasePrice <= request.MaxPrice.Value);

        if (request.MinRating.HasValue)
            query = query.Where(p => p.AvgRating >= request.MinRating.Value);

        if (request.IsFeatured.HasValue)
            query = query.Where(p => p.IsFeatured == request.IsFeatured.Value);

        // Total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = request.SortBy.ToLower() switch
        {
            "price_asc" => query.OrderBy(p => p.BasePrice),
            "price_desc" => query.OrderByDescending(p => p.BasePrice),
            "rating" => query.OrderByDescending(p => p.AvgRating ?? 0),
            "popular" => query.OrderByDescending(p => p.ViewCount),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.IsFeatured)
                      .ThenByDescending(p => p.ViewCount)
                      .ThenByDescending(p => p.AvgRating ?? 0)  // relevance
        };

        var products = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var shopIds = products.Select(p => p.ShopId).Distinct().ToList();
        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var brandIds = products.Where(p => p.BrandId.HasValue).Select(p => p.BrandId!.Value).Distinct().ToList();

        // Load shops
        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Slug }, cancellationToken);

        // Load categories
        var categories = await _context.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        // Load brands
        var brands = await _context.Brands
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        // Load primary images
        var images = await _context.ProductImages
            .Where(pi => productIds.Contains(pi.ProductId))
            .OrderBy(pi => pi.SortOrder)
            .ToListAsync(cancellationToken);

        var primaryImages = images
            .GroupBy(pi => pi.ProductId)
            .ToDictionary(g => g.Key, g => g.First().ImageUrl);

        // Load variants for price range + stock
        var variants = await _context.ProductVariants
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive && v.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var variantsByProduct = variants
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = products.Select(p =>
        {
            var productVariants = variantsByProduct.GetValueOrDefault(p.Id, new());
            var minPrice = productVariants.Count > 0 ? productVariants.Min(v => v.Price) : (decimal?)null;
            var maxPrice = productVariants.Count > 0 ? productVariants.Max(v => v.Price) : (decimal?)null;
            var totalStock = productVariants.Sum(v => v.StockQty);

            return new SearchResultDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.BasePrice,
                minPrice,
                maxPrice,
                p.AvgRating,
                p.ViewCount,
                p.IsFeatured,
                p.ShopId,
                shops.GetValueOrDefault(p.ShopId)?.Name ?? string.Empty,
                shops.GetValueOrDefault(p.ShopId)?.Slug ?? string.Empty,
                p.CategoryId,
                categories.GetValueOrDefault(p.CategoryId, string.Empty),
                p.BrandId,
                p.BrandId.HasValue ? brands.GetValueOrDefault(p.BrandId.Value) : null,
                primaryImages.GetValueOrDefault(p.Id),
                productVariants.Count,
                totalStock);
        }).ToList();

        var pagedList = new PagedList<SearchResultDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}