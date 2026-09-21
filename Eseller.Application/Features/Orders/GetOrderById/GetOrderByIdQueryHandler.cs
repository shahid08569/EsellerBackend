using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetOrderById;

public class GetOrderByIdQueryHandler
    : IRequestHandler<GetOrderByIdQuery, Result<OrderRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOrderByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<OrderRequestDto>> Handle(
        GetOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<OrderRequestDto>("User is not authenticated.", "UNAUTHENTICATED");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<OrderRequestDto>("User not found.", "USER_NOT_FOUND");

        // Get order
        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UserId == user.Id,
                cancellationToken);

        if (order is null)
            return Result.Failure<OrderRequestDto>("Order not found.", "ORDER_NOT_FOUND");

        // Get items
        var items = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);

        // Get seller orders → shops
        var sellerOrders = await _context.SellerOrders
            .Where(so => so.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);

        var sellerOrderIds = sellerOrders.Select(so => so.Id).ToList();
        var shopIds = sellerOrders.Select(so => so.ShopId).Distinct().ToList();

        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var sellerOrderByItem = sellerOrders.ToDictionary(so => so.Id);

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
            var sellerOrder = sellerOrderByItem.GetValueOrDefault(i.SellerOrderId);
            var shop = sellerOrder is null ? null : shops.GetValueOrDefault(sellerOrder.ShopId);

            return new OrderRequestItemDto(
                i.Id,
                product?.Id ?? Guid.Empty,
                i.ProductVariantId,
                shop?.Id ?? Guid.Empty,
                shop?.Name ?? string.Empty,
                shop?.Slug ?? string.Empty,
                i.ProductNameSnapshot,
                variant?.Sku ?? string.Empty,
                i.VariantAttributesSnapshot,
                i.Quantity,
                i.UnitPriceSnapshot,
                i.TotalPriceSnapshot,
                i.PriceAtOrder,
                variant?.StockQty);
        }).ToList();

        var totalAmount = itemDtos.Sum(i => i.TotalPriceSnapshot);
        var totalItems = itemDtos.Sum(i => i.Quantity);

        var dto = new OrderRequestDto(
            order.Id,
            order.Status.ToString(),
            totalAmount,
            totalItems,
            order.AffiliateId,
            order.ReferralCodeSnapshot,
            itemDtos,
            order.CreatedAt,
            order.UpdatedAt);

        return Result.Success(dto);
    }
}