using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Homepage.GetHotSellingProducts;

public class GetHotSellingProductsQueryHandler
    : IRequestHandler<GetHotSellingProductsQuery, Result<List<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductBadgeService _badgeService;

    public GetHotSellingProductsQueryHandler(
        IApplicationDbContext context,
        IProductBadgeService badgeService)
    {
        _context = context;
        _badgeService = badgeService;
    }

    public async Task<Result<List<ProductListDto>>> Handle(
        GetHotSellingProductsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit > 50 ? 50 : (request.Limit < 1 ? 20 : request.Limit);

        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        // Curated pins only — admin Hot Selling toggle must fully control this section
        var productIds = await MasterCatalog.ResolveCuratedProductIdsAsync(
            _context, masterShopId, featured: false, hotSelling: true, limit, cancellationToken);

        var products = await LoadProducts(productIds, cancellationToken);

        return Result.Success(products);
    }

    private async Task<List<ProductListDto>> LoadProducts(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new List<ProductListDto>();

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
                ShopName = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                ShopSlug = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Slug).FirstOrDefault() ?? string.Empty,
                ShopRating = _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Rating).FirstOrDefault(),
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

        var badges = await _badgeService.CalculateBadgesAsync(productIds, cancellationToken);

        return productIds
            .Select(id => products.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Select(p => new ProductListDto(
                p!.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.PrimaryImageUrl,
                p.ShopName,
                p.ShopSlug,p.CategoryName,
                p.BrandName,
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.IsApproved ? "Approved" : (p.RejectionReason == null ? "Pending" : "Rejected"),
                p.RejectionReason,
                p.CreatedAt,
                badges.GetValueOrDefault(p.Id), ShopRating: p.ShopRating))
            .ToList();
    }
}