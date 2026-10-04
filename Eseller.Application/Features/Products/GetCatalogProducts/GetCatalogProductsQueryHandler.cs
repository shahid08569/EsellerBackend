using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetCatalogProducts;

public class GetCatalogProductsQueryHandler
    : IRequestHandler<GetCatalogProductsQuery, Result<PagedList<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductBadgeService _badgeService;

    public GetCatalogProductsQueryHandler(
        IApplicationDbContext context,
        IProductBadgeService badgeService)
    {
        _context = context;
        _badgeService = badgeService;
    }

    public async Task<Result<PagedList<ProductListDto>>> Handle(
        GetCatalogProductsQuery request,
        CancellationToken cancellationToken)
    {
        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);

        var query = MasterCatalog.CatalogProducts(_context, masterShop.Id)
            .Where(p => p.IsActive && p.IsApproved);

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        if (request.BrandId.HasValue)
            query = query.Where(p => p.BrandId == request.BrandId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search)
                || (p.Description != null && p.Description.ToLower().Contains(search)));
        }

        query = request.SortBy.ToLowerInvariant() switch
        {
            "popular" => query.OrderByDescending(p => p.ViewCount),
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
                    .FirstOrDefault(),
                ShopName = masterShop.Name,
                ShopSlug = masterShop.Slug,
                CategoryName = _context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                BrandName = p.BrandId == null
                    ? null
                    : _context.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.RejectionReason,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var badges = await _badgeService.CalculateBadgesAsync(productIds, cancellationToken);

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
            "Approved",
            p.RejectionReason,
            p.CreatedAt,
            badges.GetValueOrDefault(p.Id),
            p.CategoryId,
            p.BrandId,
            p.SourceProductId,
            p.ShopId)).ToList();

        return Result.Success(new PagedList<ProductListDto>(
            productDtos,
            totalCount,
            request.PageNumber,
            request.PageSize));
    }
}
