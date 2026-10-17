using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.ProductGroups;

public record GetProductGroupByIdQuery(Guid BatchId) : IRequest<Result<ProductGroupDetailDto>>;

public class GetProductGroupByIdQueryHandler
    : IRequestHandler<GetProductGroupByIdQuery, Result<ProductGroupDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetProductGroupByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ProductGroupDetailDto>> Handle(
        GetProductGroupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var group = await _context.ProductGroupListings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == request.BatchId && g.DeletedAt == null, cancellationToken);

        if (group is null)
            return Result.Failure<ProductGroupDetailDto>("Product group not found.", "GROUP_NOT_FOUND");

        if (!await PartnerScopeHelper.IsShopPermittedForPartnerAsync(_context, _currentUserService, group.ShopId, cancellationToken))
            return Result.Failure<ProductGroupDetailDto>("Forbidden: You can only view product groups for shops registered through your referral link.", "FORBIDDEN");

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == group.ShopId, cancellationToken);

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ListingBatchId == group.Id && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();

        var images = await _context.ProductImages
            .Where(pi => productIds.Contains(pi.ProductId) && pi.IsCover)
            .ToDictionaryAsync(pi => pi.ProductId, pi => pi.ImageUrl, cancellationToken);

        var fallbackImages = await _context.ProductImages
            .Where(pi => productIds.Contains(pi.ProductId))
            .OrderBy(pi => pi.SortOrder)
            .ToListAsync(cancellationToken);

        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var categories = await _context.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var brandIds = products.Where(p => p.BrandId.HasValue).Select(p => p.BrandId!.Value).Distinct().ToList();
        var brands = await _context.Brands
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        var variantCounts = await _context.ProductVariants
            .Where(pv => productIds.Contains(pv.ProductId) && pv.DeletedAt == null)
            .GroupBy(pv => pv.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, cancellationToken);

        var cardDtos = products.Select(p =>
        {
            images.TryGetValue(p.Id, out var img);
            if (string.IsNullOrWhiteSpace(img))
            {
                img = fallbackImages.FirstOrDefault(x => x.ProductId == p.Id)?.ImageUrl;
            }

            categories.TryGetValue(p.CategoryId, out var catName);
            string? brandName = null;
            if (p.BrandId.HasValue)
            {
                brands.TryGetValue(p.BrandId.Value, out brandName);
            }

            variantCounts.TryGetValue(p.Id, out var vCount);

            return new GroupProductCardDto(
                p.Id,
                p.SourceProductId,
                p.Name,
                p.Slug,
                p.BasePrice,
                img,
                p.CategoryId,
                catName ?? "Category",
                p.BrandId,
                brandName ?? "Brand",
                p.IsApproved,
                p.IsActive,
                vCount,
                p.CreatedAt
            );
        }).ToList();

        var approvedCount = cardDtos.Count(p => p.IsApproved);
        var pendingCount = cardDtos.Count(p => !p.IsApproved);
        var isApproved = cardDtos.Count > 0 && pendingCount == 0;

        var detail = new ProductGroupDetailDto(
            group.Id,
            group.ShopId,
            shop?.Name ?? "Shop",
            shop?.Slug ?? "shop",
            group.BatchName,
            group.RequestedCount,
            group.DeliveredCount,
            cardDtos.Count,
            approvedCount,
            pendingCount,
            isApproved,
            group.CategoryFilter,
            group.BrandFilter,
            group.MinPrice,
            group.MaxPrice,
            group.CreatedAt,
            group.UpdatedAt,
            cardDtos
        );

        return Result.Success(detail);
    }
}
