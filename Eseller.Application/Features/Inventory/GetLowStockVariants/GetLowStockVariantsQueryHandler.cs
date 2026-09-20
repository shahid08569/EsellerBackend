using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Inventory.GetLowStockVariants;

public class GetLowStockVariantsQueryHandler
    : IRequestHandler<GetLowStockVariantsQuery, Result<IReadOnlyList<LowStockVariantDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLowStockVariantsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<LowStockVariantDto>>> Handle(
        GetLowStockVariantsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<LowStockVariantDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure<IReadOnlyList<LowStockVariantDto>>(
                "Shop not found.", "SHOP_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<IReadOnlyList<LowStockVariantDto>>(
                    "You do not have permission.", "FORBIDDEN");
        }

        // Get all active variants of products in this shop where StockQty <= LowStockThreshold
        var lowStockVariants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => pv.DeletedAt == null && pv.IsActive)
            .Join(_context.Products.Where(p => p.ShopId == request.ShopId && p.DeletedAt == null),
                  pv => pv.ProductId,
                  p => p.Id,
                  (pv, p) => new { pv, p })
            .Where(x => x.pv.StockQty <= x.pv.LowStockThreshold)
            .OrderBy(x => x.pv.StockQty)
            .Select(x => new LowStockVariantDto(
                x.pv.Id,
                x.p.Id,
                x.p.Name,
                x.pv.Sku,
                x.pv.StockQty,
                x.pv.LowStockThreshold,
                x.pv.ImageUrl))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LowStockVariantDto>>(lowStockVariants);
    }
}