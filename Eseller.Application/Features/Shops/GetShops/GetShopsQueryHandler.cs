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

        // Public directory: hide master warehouse + shops with no live storefront products
        var query = _context.Shops
            .AsNoTracking()
            .Where(s => s.DeletedAt == null && s.IsActive)
            .Where(s => !masterShopId.HasValue || s.Id != masterShopId.Value)
            .Where(s => s.Slug != MasterCatalog.ShopSlug && s.Name != MasterCatalog.ShopName)
            .Where(s => _context.Products.Any(p =>
                p.ShopId == s.Id
                && p.IsActive
                && p.IsApproved
                && p.DeletedAt == null));

        if (request.ShopCategoryId.HasValue)
        {
            query = query.Where(s => s.ShopCategoryId == request.ShopCategoryId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Single page query — use denormalized Shop.TotalProducts (updated on create/add-to-store).
        // Avoids a second heavy GroupBy that was aborting with TaskCanceledException on client disconnect.
        var shopsWithCategory = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .GroupJoin(
                _context.ShopCategories.AsNoTracking().Where(sc => sc.DeletedAt == null),
                s => s.ShopCategoryId,
                sc => sc.Id,
                (s, scList) => new { Shop = s, Category = scList.FirstOrDefault() })
            .ToListAsync(cancellationToken);

        var shops = shopsWithCategory.Select(x => new ShopListDto(
            x.Shop.Id,
            x.Shop.Name,
            x.Shop.Slug,
            x.Shop.LogoUrl,
            x.Shop.City,
            x.Shop.Country,
            x.Shop.Rating,
            x.Shop.TotalProducts,
            x.Shop.CreatedAt,
            x.Shop.ShopCategoryId,
            x.Category != null ? x.Category.Name : null,
            !string.IsNullOrWhiteSpace(x.Shop.CustomBadgeText) ? x.Shop.CustomBadgeText : (x.Category != null ? x.Category.BadgeText : null),
            x.Category != null ? x.Category.BadgeColor : null,
            x.Category != null ? x.Category.IconUrl : null
        )).ToList();

        var pagedList = new PagedList<ShopListDto>(
            shops,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}
