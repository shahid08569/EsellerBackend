using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.GetShops;

public class GetShopsQueryHandler
    : IRequestHandler<GetShopsQuery, Result<PagedList<ShopListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetShopsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<ShopListDto>>> Handle(
        GetShopsQuery request,
        CancellationToken cancellationToken)
    {
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        // Public directory: hide master warehouse. Use denormalized TotalProducts
        // (no correlated Products.Any — that was aborting CountAsync on dashboard reload).
        var query = _context.Shops
            .AsNoTracking()
            .Where(s => s.DeletedAt == null && s.IsActive && s.TotalProducts > 0)
            .Where(s => !masterShopId.HasValue || s.Id != masterShopId.Value)
            .Where(s => s.Slug != MasterCatalog.ShopSlug && s.Name != MasterCatalog.ShopName);

        if (request.ShopCategoryId.HasValue)
            query = query.Where(s => s.ShopCategoryId == request.ShopCategoryId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageShops = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var categoryIds = pageShops
            .Where(s => s.ShopCategoryId.HasValue)
            .Select(s => s.ShopCategoryId!.Value)
            .Distinct()
            .ToList();

        var categories = categoryIds.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Catalog.ShopCategory>()
            : await _context.ShopCategories
                .AsNoTracking()
                .Where(sc => categoryIds.Contains(sc.Id) && sc.DeletedAt == null)
                .ToDictionaryAsync(sc => sc.Id, cancellationToken);

        var shops = pageShops.Select(s =>
        {
            categories.TryGetValue(s.ShopCategoryId ?? Guid.Empty, out var cat);
            return new ShopListDto(
                s.Id,
                s.Name,
                s.Slug,
                s.LogoUrl,
                s.City,
                s.Country,
                s.Rating,
                s.TotalProducts,
                s.CreatedAt,
                s.ShopCategoryId,
                cat?.Name,
                !string.IsNullOrWhiteSpace(s.CustomBadgeText) ? s.CustomBadgeText : cat?.BadgeText,
                cat?.BadgeColor,
                cat?.IconUrl);
        }).ToList();

        return Result.Success(new PagedList<ShopListDto>(
            shops,
            totalCount,
            request.PageNumber,
            request.PageSize));
    }
}
