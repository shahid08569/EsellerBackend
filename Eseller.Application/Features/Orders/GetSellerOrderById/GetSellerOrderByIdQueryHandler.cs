using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetSellerOrderById;

public class GetSellerOrderByIdQueryHandler
    : IRequestHandler<GetSellerOrderByIdQuery, Result<SellerOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSellerOrderByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SellerOrderDto>> Handle(
        GetSellerOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<SellerOrderDto>("User is not authenticated.", "UNAUTHENTICATED");

        var sellerOrder = await _context.SellerOrders
            .FirstOrDefaultAsync(so => so.Id == request.SellerOrderId, cancellationToken);

        if (sellerOrder is null)
            return Result.Failure<SellerOrderDto>("Seller order not found.", "SELLER_ORDER_NOT_FOUND");

        // Permission
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isShopkeeper = role == RoleType.Shopkeeper;

        if (!isAdmin)
        {
            if (!isShopkeeper)
                return Result.Failure<SellerOrderDto>("You do not have permission.", "FORBIDDEN");

            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == sellerOrder.ShopId, cancellationToken);

            if (shop is null)
                return Result.Failure<SellerOrderDto>("Shop not found.", "SHOP_NOT_FOUND");

            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<SellerOrderDto>("You do not have permission.", "FORBIDDEN");
        }

        // Load items
        var items = await _context.OrderRequestItems
            .Where(i => i.SellerOrderId == sellerOrder.Id)
            .ToListAsync(cancellationToken);

        var shopEntity = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == sellerOrder.ShopId, cancellationToken);

        var variantIds = items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id))
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var itemDtos = items.Select(i =>
        {
            var variant = variants.GetValueOrDefault(i.ProductVariantId);
            var product = variant is null ? null : products.GetValueOrDefault(variant.ProductId);

            return new OrderRequestItemDto(
                i.Id,
                product?.Id ?? Guid.Empty,
                i.ProductVariantId,
                sellerOrder.ShopId,
                shopEntity?.Name ?? string.Empty,
                shopEntity?.Slug ?? string.Empty,
                i.ProductNameSnapshot,
                variant?.Sku ?? string.Empty,
                i.VariantAttributesSnapshot,
                i.Quantity,
                i.UnitPriceSnapshot,
                i.TotalPriceSnapshot,
                i.PriceAtOrder,
                variant?.StockQty);
        }).ToList();

        var dto = new SellerOrderDto(
            sellerOrder.Id,
            sellerOrder.OrderRequestId,
            sellerOrder.ShopId,
            shopEntity?.Name ?? string.Empty,
            shopEntity?.Slug ?? string.Empty,
            sellerOrder.Status.ToString(),
            itemDtos.Sum(i => i.TotalPriceSnapshot),
            itemDtos.Sum(i => i.Quantity),
            itemDtos,
            sellerOrder.CreatedAt,
            sellerOrder.UpdatedAt);

        return Result.Success(dto);
    }
}