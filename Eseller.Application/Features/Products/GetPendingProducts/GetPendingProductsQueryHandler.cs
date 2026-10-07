using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetPendingProducts;

public class GetPendingProductsQueryHandler
    : IRequestHandler<GetPendingProductsQuery, Result<PagedList<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<ProductListDto>>> Handle(
        GetPendingProductsQuery request,
        CancellationToken cancellationToken)
    {
        // Pending seller listings only (never master warehouse catalog SKUs)
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        var query = _context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                !p.IsApproved
                && p.IsActive
                && p.DeletedAt == null
                && p.RejectionReason == null
                && (!masterShopId.HasValue || p.ShopId != masterShopId.Value));

        var kind = request.Kind?.Trim().ToLowerInvariant();
        if (kind is "global" or "warehouse" or "listing")
            query = query.Where(p => p.SourceProductId != null);
        else if (kind is "own" or "seller" or "new")
            query = query.Where(p => p.SourceProductId == null);

        query = query.OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                _context.ProductImages
                    .Where(pi => pi.ProductId == p.Id)
                    .OrderBy(pi => pi.SortOrder)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault(),
                _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Slug).FirstOrDefault() ?? string.Empty,
                _context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                p.BrandId == null
                    ? null
                    : _context.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                "Pending",
                p.RejectionReason,
                p.CreatedAt,
                null,
                p.CategoryId,
                p.BrandId,
                p.SourceProductId,
                p.ShopId))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProductListDto>(
            products,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}