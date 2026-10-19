using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.UpdateOrderStatus;

public class UpdateOrderStatusCommandHandler
    : IRequestHandler<UpdateOrderStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public UpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notifications = notifications;
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

        // Block status updates on chat/support channel rows (not commerce orders)
        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure("Order not found.", "ORDER_NOT_FOUND");

        if (role == RoleType.Partner)
        {
            var hasPerm = await PartnerPermissionChecker.HasPermissionAsync(
                _context, _currentUserService, "orders:manage", cancellationToken);
            if (!hasPerm)
                return Result.Failure("You do not have permission to update order status.", "FORBIDDEN");

            var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            var isPartnerOrder = await _context.SellerOrders
                .AnyAsync(so => so.OrderRequestId == order.Id && partnerShopIds != null && partnerShopIds.Contains(so.ShopId), cancellationToken);

            if (!isPartnerOrder)
                return Result.Failure("Partners can only manage orders for their referred shops.", "FORBIDDEN");
        }

        if (IsChatChannel(order.ShippingAddress))
            return Result.Failure("This is a chat channel, not a commerce order.", "NOT_A_COMMERCE_ORDER");

        var oldStatus = order.Status;
        var newStatus = request.NewStatus;

        if (oldStatus == newStatus)
            return Result.Failure("Order is already in this status.", "NO_CHANGE");

        if (!IsValidTransition(oldStatus, newStatus))
            return Result.Failure(
                $"Cannot change status from {oldStatus} to {newStatus}.",
                "INVALID_STATUS_TRANSITION");

        var now = _dateTime.UtcNow;

        order.Status = newStatus;
        order.SetUpdatedAt(now);

        // Keep every linked SellerOrder in sync for ALL status transitions
        var sellerOrders = await _context.SellerOrders
            .Where(so => so.OrderRequestId == order.Id && so.Status != newStatus)
            .ToListAsync(cancellationToken);

        foreach (var so in sellerOrders)
        {
            so.Status = newStatus;
            so.SetUpdatedAt(now);
        }

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

        // Credit merchant earnings ONLY when order is Delivered AND payment is Paid (Completed).
        List<(Guid AccountId, decimal Credit)> creditedSellers = [];
        if (newStatus == OrderStatus.Delivered)
        {
            var isPaid = await _context.Payments
                .Where(p => p.OrderRequestId == order.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.Status == PaymentStatus.Completed)
                .FirstOrDefaultAsync(cancellationToken);

            if (isPaid)
            {
                try
                {
                    var allSellerOrderIds = await _context.SellerOrders
                        .Where(so => so.OrderRequestId == order.Id)
                        .Select(so => so.Id)
                        .ToListAsync(cancellationToken);

                    creditedSellers = await SellerEarningsWalletEffects.ReleasePendingToAvailableAsync(
                        _context, _dateTime, order.Id, cancellationToken, allSellerOrderIds);
                }
                catch (Exception ex)
                {
                    return Result.Failure(
                        $"Order delivered wallet credit failed: {ex.GetBaseException().Message}",
                        "WALLET_CREDIT_FAILED");
                }
            }
        }
        else if (newStatus == OrderStatus.Cancelled)
        {
            var sellerOrderIds = await _context.SellerOrders
                .Where(so => so.OrderRequestId == order.Id)
                .Select(so => so.Id)
                .ToListAsync(cancellationToken);
            await SellerEarningsWalletEffects.ReversePendingAsync(
                _context, _dateTime, sellerOrderIds, cancellationToken);
        }
        else if (newStatus == OrderStatus.Refunded)
        {
            var sellerOrderIds = await _context.SellerOrders
                .Where(so => so.OrderRequestId == order.Id)
                .Select(so => so.Id)
                .ToListAsync(cancellationToken);
            // Pre-delivery refund: drop pending. Post-delivery refund: claw back available + balance.
            await SellerEarningsWalletEffects.ReversePendingAsync(
                _context, _dateTime, sellerOrderIds, cancellationToken);
            await SellerEarningsWalletEffects.ClawBackAvailableAsync(
                _context, _dateTime, sellerOrderIds,
                "Seller earning clawed back for refunded order",
                cancellationToken);
        }

        // Affiliate commission side-effects
        var pendingCommissions = await _context.AffiliateCommissions
            .Where(c => c.OrderRequestId == order.Id
                     && c.Status == AffiliateCommissionStatus.Pending)
            .ToListAsync(cancellationToken);

        var approvedCommissions = await _context.AffiliateCommissions
            .Where(c => c.OrderRequestId == order.Id
                     && c.Status == AffiliateCommissionStatus.Approved)
            .ToListAsync(cancellationToken);

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
                            Description = $"Commission reversed for refunded order {commission.OrderRequestId.ToString()[..8]}",
                            CreatedAt = now
                        });
                    }
                }
            }
        }

        _context.AuditLogs.Add(new Eseller.Domain.Entities.Identity.AuditLog
        {
            Id = Guid.NewGuid(),
            ActorAccountId = _currentUserService.AccountId,
            ActorType = _currentUserService.RoleType.ToString(),
            Action = "UPDATE_ORDER_STATUS",
            EntityType = "Order",
            EntityId = order.Id,
            OldValues = $"Status: {oldStatus}",
            NewValues = $"Status: {newStatus}",
            IpAddress = null,
            CreatedAt = now
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure(
                $"Could not save order status: {ex.GetBaseException().Message}",
                "ORDER_STATUS_SAVE_FAILED");
        }

        // Notifications are best-effort — never fail a successful Delivered save.
        if (newStatus == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered)
        {
            try
            {
                await NotifyDeliveredAsync(order.Id, creditedSellers, cancellationToken);
            }
            catch
            {
                // Status already persisted; ignore notification failures.
            }
        }

        return Result.Success();
    }

    private async Task NotifyDeliveredAsync(
        Guid orderId,
        List<(Guid AccountId, decimal Credit)> creditedSellers,
        CancellationToken cancellationToken)
    {
        var shortRef = orderId.ToString()[..8].ToUpperInvariant();
        var sellerAccounts = await (
            from so in _context.SellerOrders.AsNoTracking()
            join s in _context.Shops.IgnoreQueryFilters().AsNoTracking() on so.ShopId equals s.Id
            join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where so.OrderRequestId == orderId && s.DeletedAt == null
            select new { so.Id, sk.AccountId }
        ).Distinct().ToListAsync(cancellationToken);

        foreach (var seller in sellerAccounts)
        {
            await _notifications.NotifyAsync(
                seller.AccountId,
                RoleType.Shopkeeper,
                NotificationType.OrderDelivered,
                $"Order #{shortRef} delivered",
                "An order has been marked as delivered.",
                NotificationEntityType.Order,
                orderId,
                "/orders",
                $"OrderDelivered:{seller.Id:N}:Seller:{seller.AccountId:N}",
                cancellationToken);
        }

        foreach (var (accountId, credit) in creditedSellers)
        {
            await _notifications.NotifyAsync(
                accountId,
                RoleType.Shopkeeper,
                NotificationType.PaymentCompleted,
                "Payment Completed",
                $"Payment for order #{shortRef} has been completed. ${credit:N0} credited to your wallet.",
                NotificationEntityType.Payment,
                orderId,
                "/earnings",
                $"PaymentCompleted:{orderId:N}:Seller:{accountId:N}",
                cancellationToken);

            await _notifications.NotifyAsync(
                accountId,
                RoleType.Shopkeeper,
                NotificationType.CommissionEarned,
                "Commission Earned",
                $"Congratulations! You earned ${credit:N0} commission from order #{shortRef}.",
                NotificationEntityType.Earning,
                orderId,
                "/earnings",
                $"CommissionEarned:{orderId:N}:Seller:{accountId:N}",
                cancellationToken);
        }
    }

    private static bool IsChatChannel(string? shippingAddress)
    {
        if (string.IsNullOrWhiteSpace(shippingAddress))
            return false;

        return shippingAddress.StartsWith("SHOP_SUPPORT_", StringComparison.OrdinalIgnoreCase)
            || shippingAddress.StartsWith("SHOP_CHAT_", StringComparison.OrdinalIgnoreCase)
            || shippingAddress.StartsWith("CUSTOMER_SUPPORT_", StringComparison.OrdinalIgnoreCase)
            || shippingAddress.Equals("Live Support Inquiry", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        if (to == OrderStatus.Cancelled) return from != OrderStatus.Delivered && from != OrderStatus.Refunded;
        if (from == OrderStatus.Cancelled && to != OrderStatus.Cancelled) return false;

        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Confirmed, OrderStatus.OnTheWay) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Confirmed, OrderStatus.Delivered) => true,
            (OrderStatus.Processing, OrderStatus.OnTheWay) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Processing, OrderStatus.Delivered) => true,
            (OrderStatus.OnTheWay, OrderStatus.Shipped) => true,
            (OrderStatus.OnTheWay, OrderStatus.OutForDelivery) => true,
            (OrderStatus.OnTheWay, OrderStatus.Delivered) => true,
            (OrderStatus.Shipped, OrderStatus.OutForDelivery) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Delivered) => true,
            (OrderStatus.Delivered, OrderStatus.ReturnRequested) => true,
            (OrderStatus.Delivered, OrderStatus.Returned) => true,
            (OrderStatus.ReturnRequested, OrderStatus.Returned) => true,
            (OrderStatus.Returned, OrderStatus.RefundPending) => true,
            (OrderStatus.Returned, OrderStatus.Refunded) => true,
            (OrderStatus.RefundPending, OrderStatus.Refunded) => true,
            _ => false
        };
    }
}
