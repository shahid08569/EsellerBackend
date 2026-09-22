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

        if (!IsValidTransition(oldStatus, newStatus))
            return Result.Failure(
                $"Cannot change status from {oldStatus} to {newStatus}.",
                "INVALID_STATUS_TRANSITION");

        var now = _dateTime.UtcNow;

        // Update order status
        order.Status = newStatus;
        order.SetUpdatedAt(now);

        // Log status history
        var changedByType = role == RoleType.SuperAdmin ? "SuperAdmin" : "Partner";

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderRequestId = order.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByAccountId = _currentUserService.AccountId.Value,
            ChangedByType = changedByType,
            ChangedAt = now
        });

        // Auto chat message
        var statusMessage = BuildStatusMessage(newStatus);

        _context.ChatMessages.Add(new ChatMessageEntity
        {
            OrderRequestId = order.Id,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = role == RoleType.SuperAdmin ? ChatSenderRole.SuperAdmin : ChatSenderRole.Partner,
            Message = statusMessage,
            SentAt = now,
            IsRead = false
        });

        // ═══════════════════════════════════════════════════════════
        // PART 4 — AFFILIATE COMMISSION UPDATE
        // ═══════════════════════════════════════════════════════════
     
        var pendingCommissions = await _context.AffiliateCommissions
            .Where(c => c.OrderRequestId == order.Id
                     && c.Status == AffiliateCommissionStatus.Pending)
            .ToListAsync(cancellationToken);

        var approvedCommissions = await _context.AffiliateCommissions
            .Where(c => c.OrderRequestId == order.Id
                     && c.Status == AffiliateCommissionStatus.Approved)
            .ToListAsync(cancellationToken);

        // DELIVERED → Kuch nahi (SuperAdmin manually approve karega)
        // (koi code nahi)

        // CANCELLED → Reverse pending + TotalConversions -1
        if (newStatus == OrderStatus.Cancelled && pendingCommissions.Count > 0)
        {
            foreach (var commission in pendingCommissions)
            {
                commission.Status = AffiliateCommissionStatus.Reversed;

                var affiliate = await _context.Affiliates
                    .FirstOrDefaultAsync(a => a.Id == commission.AffiliateId, cancellationToken);

                if (affiliate is not null && affiliate.TotalConversions > 0)
                {
                    affiliate.TotalConversions -= 1;
                    affiliate.SetUpdatedAt(now);
                }
            }
        }

        // REFUNDED → Reverse approved + TotalEarned reverse + Wallet debit
        else if (newStatus == OrderStatus.Refunded && approvedCommissions.Count > 0)
        {
            foreach (var commission in approvedCommissions)
            {
                commission.Status = AffiliateCommissionStatus.Reversed;

                var affiliate = await _context.Affiliates
                    .FirstOrDefaultAsync(a => a.Id == commission.AffiliateId, cancellationToken);

                if (affiliate is not null)
                {
                    affiliate.TotalEarned -= commission.CommissionAmount;
                    affiliate.SetUpdatedAt(now);

                    var wallet = await _context.Wallets
                        .FirstOrDefaultAsync(w => w.AccountId == affiliate.AccountId, cancellationToken);

                    if (wallet is not null)
                    {
                        wallet.Balance -= commission.CommissionAmount;
                        wallet.SetUpdatedAt(now);

                        _context.WalletTransactions.Add(new Eseller.Domain.Entities.Wallet.WalletTransaction
                        {
                            WalletId = wallet.Id,
                            Direction = WalletDirection.Debit,
                            TransactionType = WalletTransactionType.AffiliateCommission,
                            Amount = commission.CommissionAmount,
                            ReferenceType = "OrderRequest",
                            ReferenceId = commission.OrderRequestId,
                            Description = $"Commission reversed for refunded order {commission.OrderRequestId.ToString().Substring(0, 8)}",
                            CreatedAt = now
                        });
                    }
                }
            }
        }

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