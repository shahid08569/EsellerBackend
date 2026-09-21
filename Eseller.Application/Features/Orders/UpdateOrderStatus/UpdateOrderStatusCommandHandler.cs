using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ChatMessageEntity = Eseller.Domain.Entities.Orders.ChatMessage;

namespace Eseller.Application.Features.Orders.UpdateOrderStatus;

public class UpdateOrderStatusCommandHandler
    : IRequestHandler<UpdateOrderStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        // Only admin
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can update order status.", "FORBIDDEN");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure("Order not found.", "ORDER_NOT_FOUND");

        var oldStatus = order.Status;
        var newStatus = request.NewStatus;

        if (oldStatus == newStatus)
            return Result.Failure("Order is already in this status.", "NO_CHANGE");

        // Validate transition
        if (!IsValidTransition(oldStatus, newStatus))
            return Result.Failure(
                $"Cannot change status from {oldStatus} to {newStatus}.",
                "INVALID_STATUS_TRANSITION");

        // Update order status
        order.Status = newStatus;
        order.SetUpdatedAt(_dateTime.UtcNow);

        // Log status history
        var changedByType = role == RoleType.SuperAdmin ? "SuperAdmin" : "Partner";

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderRequestId = order.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByAccountId = _currentUserService.AccountId.Value,
            ChangedByType = changedByType,
            ChangedAt = _dateTime.UtcNow
        });

        // Auto chat message
        var statusMessage = BuildStatusMessage(newStatus);

        _context.ChatMessages.Add(new ChatMessageEntity
        {
            OrderRequestId = order.Id,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = role == RoleType.SuperAdmin ? ChatSenderRole.SuperAdmin : ChatSenderRole.Partner,
            Message = statusMessage,
            SentAt = _dateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Packed) => true,
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,
            (OrderStatus.Packed, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.OutForDelivery) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Delivered) => true,
            (OrderStatus.Delivered, OrderStatus.ReturnRequested) => true,
            (OrderStatus.ReturnRequested, OrderStatus.Returned) => true,
            (OrderStatus.Returned, OrderStatus.RefundPending) => true,
            (OrderStatus.RefundPending, OrderStatus.Refunded) => true,
            _ => false
        };
    }

    private static string BuildStatusMessage(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Confirmed => "✅ Your order has been confirmed. We'll start processing it soon.",
            OrderStatus.Processing => "⚙️ Your order is being processed.",
            OrderStatus.Packed => "📦 Your order has been packed and is ready for shipping.",
            OrderStatus.Shipped => "🚚 Your order has been shipped.",
            OrderStatus.OutForDelivery => "🛵 Your order is out for delivery.",
            OrderStatus.Delivered => "✅ Your order has been delivered. Thank you!",
            OrderStatus.Cancelled => "❌ Your order has been cancelled.",
            OrderStatus.ReturnRequested => "🔄 Return requested. We'll review it.",
            OrderStatus.Returned => "📦 Return received. Refund will be processed.",
            OrderStatus.RefundPending => "💰 Refund is being processed.",
            OrderStatus.Refunded => "✅ Refund has been completed.",
            _ => $"Order status updated to {status}."
        };
    }
}