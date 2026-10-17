using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetRejectedProducts;

public class GetRejectedProductsQueryHandler
    : IRequestHandler<GetRejectedProductsQuery, Result<PagedList<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetRejectedProductsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ProductListDto>>> Handle(
        GetRejectedProductsQuery request,
        CancellationToken cancellationToken)
    {
        // Rejected seller listings (clones + seller-created originals; exclude master warehouse)
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        var query = _context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                !p.IsApproved
                && p.DeletedAt == null
                && p.RejectionReason != null
                && (!masterShopId.HasValue || p.ShopId != masterShopId.Value));

        var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
        if (partnerShopIds != null)
        {
            query = query.Where(p => partnerShopIds.Contains(p.ShopId));
        }

        query = query.OrderByDescending(p => p.UpdatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return Result.Success(new PagedList<ProductListDto>(new List<ProductListDto>(), 0, request.PageNumber, request.PageSize));
        }

        var rawProducts = await query
            .AsNoTracking()
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.RejectionReason,
                p.CreatedAt,
                p.CategoryId,
                p.BrandId,
                p.SourceProductId,
                p.ShopId
            })
            .ToListAsync(cancellationToken);

        var productIds = rawProducts.Select(p => p.Id).ToList();
        var shopIds = rawProducts.Select(p => p.ShopId).Distinct().ToList();
        var categoryIds = rawProducts.Select(p => p.CategoryId).Distinct().ToList();
        var brandIds = rawProducts.Where(p => p.BrandId.HasValue).Select(p => p.BrandId!.Value).Distinct().ToList();

        var shops = await _context.Shops
            .AsNoTracking()
            .Where(s => shopIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Slug })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var brands = await _context.Brands
            .AsNoTracking()
            .Where(b => brandIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Name })
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        var images = await _context.ProductImages
            .AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .OrderByDescending(pi => pi.IsCover)
            .ThenBy(pi => pi.SortOrder)
            .Select(pi => new { pi.ProductId, pi.ImageUrl })
            .ToListAsync(cancellationToken);

        var imageMap = images
            .GroupBy(pi => pi.ProductId)
            .ToDictionary(g => g.Key, g => g.First().ImageUrl);

        var products = rawProducts.Select(p =>
        {
            shops.TryGetValue(p.ShopId, out var shop);
            categories.TryGetValue(p.CategoryId, out var cat);
            string? brandName = null;
            if (p.BrandId.HasValue && brands.TryGetValue(p.BrandId.Value, out var brand))
            {
                brandName = brand.Name;
            }
            imageMap.TryGetValue(p.Id, out var img);

            return new ProductListDto(
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                img,
                shop?.Name ?? string.Empty,
                shop?.Slug ?? string.Empty,
                cat?.Name ?? string.Empty,
                brandName,
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                "Rejected",
                p.RejectionReason,
                p.CreatedAt,
                null,
                p.CategoryId,
                p.BrandId,
                p.SourceProductId,
                p.ShopId
            );
        }).ToList();

        var pagedList = new PagedList<ProductListDto>(
            products,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}
