using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Inventory.GetVariantInventory;

public class GetVariantInventoryQueryHandler
    : IRequestHandler<GetVariantInventoryQuery, Result<VariantInventoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetVariantInventoryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<VariantInventoryDto>> Handle(
        GetVariantInventoryQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<VariantInventoryDto>("User is not authenticated.", "UNAUTHENTICATED");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<VariantInventoryDto>("Product not found.", "PRODUCT_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

            var shopkeeper = shop is null ? null : await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<VariantInventoryDto>("You do not have permission.", "FORBIDDEN");
        }

        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.VariantId
                                    && pv.ProductId == request.ProductId
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure<VariantInventoryDto>("Variant not found.", "VARIANT_NOT_FOUND");

        var dto = new VariantInventoryDto(
            variant.Id,
            variant.Sku,
            variant.StockQty,
            variant.LowStockThreshold,
            variant.StockQty <= variant.LowStockThreshold);

        return Result.Success(dto);
    }
}