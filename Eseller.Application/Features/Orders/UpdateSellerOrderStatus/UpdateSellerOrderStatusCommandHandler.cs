using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.UpdateSellerOrderStatus;

public class UpdateSellerOrderStatusCommandHandler
    : IRequestHandler<UpdateSellerOrderStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateSellerOrderStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateSellerOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isShopkeeper = role == RoleType.Shopkeeper;

        if (!isAdmin && !isShopkeeper)
            return Result.Failure("You do not have permission.", "FORBIDDEN");

        var sellerOrder = await _context.SellerOrders
            .FirstOrDefaultAsync(so => so.Id == request.SellerOrderId, cancellationToken);

        if (sellerOrder is null)
            return Result.Failure("Seller order not found.", "SELLER_ORDER_NOT_FOUND");

        // Shopkeeper ownership check
        if (isShopkeeper)
        {
            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == sellerOrder.ShopId, cancellationToken);

            if (shop is null)
                return Result.Failure("Shop not found.", "SHOP_NOT_FOUND");

            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        var oldStatus = sellerOrder.Status;
        var newStatus = request.NewStatus;

        if (oldStatus == newStatus)
            return Result.Failure("Seller order is already in this status.", "NO_CHANGE");

        if (!IsValidTransition(oldStatus, newStatus))
            return Result.Failure(
                $"Cannot change status from {oldStatus} to {newStatus}.",
                "INVALID_STATUS_TRANSITION");

        sellerOrder.Status = newStatus;
        sellerOrder.SetUpdatedAt(_dateTime.UtcNow);

        // Log status history (order-level)
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderRequestId = sellerOrder.OrderRequestId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByAccountId = _currentUserService.AccountId.Value,
            ChangedByType = isAdmin
                ? (role == RoleType.SuperAdmin ? "SuperAdmin" : "Partner")
                : "Shopkeeper",
            ChangedAt = _dateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Processing, OrderStatus.Packed) => true,
            (OrderStatus.Packed, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.OutForDelivery) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Delivered) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            _ => false
        };
    }
}