using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Homepage.GetFlashSaleProducts;

public class GetFlashSaleProductsQueryHandler
    : IRequestHandler<GetFlashSaleProductsQuery, Result<List<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductBadgeService _badgeService;
    private readonly IDateTime _dateTime;

    public GetFlashSaleProductsQueryHandler(
        IApplicationDbContext context,
        IProductBadgeService badgeService,
        IDateTime dateTime)
    {
        _context = context;
        _badgeService = badgeService;
        _dateTime = dateTime;
    }

    public async Task<Result<List<ProductListDto>>> Handle(
        GetFlashSaleProductsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit > 50 ? 50 : (request.Limit < 1 ? 20 : request.Limit);
        var now = _dateTime.UtcNow;
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        var rawIds = await _context.FlashSaleProducts
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

        if (rawIds.Count == 0)
            return Result.Success(new List<ProductListDto>());

        // Only products already listed on a seller shop
        var productIds = await MasterCatalog.MapToStorefrontOrSelfAsync(
            _context, masterShopId, rawIds, limit, cancellationToken);

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
            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.IsApproved)
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

        return products.Select(p => new ProductListDto(
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
            p.IsApproved ? "Approved" : "Pending",
            p.RejectionReason,
            p.CreatedAt,
            badges.GetValueOrDefault(p.Id)))
            .ToList();
    }
}