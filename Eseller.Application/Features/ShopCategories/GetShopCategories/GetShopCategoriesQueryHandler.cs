using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ShopCategories.GetShopCategories;

public class GetShopCategoriesQueryHandler
    : IRequestHandler<GetShopCategoriesQuery, Result<IReadOnlyList<ShopCategoryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetShopCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<ShopCategoryDto>>> Handle(
        GetShopCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ShopCategories
            .Where(sc => sc.DeletedAt == null);

        if (request.OnlyActive)
            query = query.Where(sc => sc.IsActive);

        var categories = await query
            .OrderBy(sc => sc.DisplayOrder)
            .ThenBy(sc => sc.Name)
            .ToListAsync(cancellationToken);

        var categoryIds = categories.Select(c => c.Id).ToList();

        var shopCounts = await _context.Shops
            .Where(s => s.ShopCategoryId.HasValue && categoryIds.Contains(s.ShopCategoryId.Value) && s.DeletedAt == null)
            .GroupBy(s => s.ShopCategoryId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var result = categories.Select(c => new ShopCategoryDto(
            c.Id,
            c.Name,
            c.Slug,
            c.Description,
            c.BadgeText,
            c.BadgeColor,
            c.IconUrl,
            c.DisplayOrder,
            c.IsActive,
            shopCounts.GetValueOrDefault(c.Id, 0),
            c.CreatedAt,
            c.PriceUsd,
            c.MaxProductListings > 0 ? c.MaxProductListings : 200)).ToList();

        return Result.Success<IReadOnlyList<ShopCategoryDto>>(result);
    }
}
