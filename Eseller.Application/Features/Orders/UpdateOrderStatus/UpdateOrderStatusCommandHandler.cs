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
    private readonly ISignalRNotificationService _notificationService;

    public UpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notificationService = notificationService;
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

        // Credit merchant earnings when order is Delivered (admin path).
        // No chat messages on commerce orders — status goes via notifications only.
        if (newStatus == OrderStatus.Delivered)
        {
            await CreditSellerEarningsForOrderAsync(order.Id, now, cancellationToken);
        }
        else if (newStatus == OrderStatus.Cancelled || newStatus == OrderStatus.Refunded)
        {
            await ReversePendingSellerEarningsForOrderAsync(order.Id, cancellationToken);
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

        // Notify linked shopkeepers + customer
        var notifyAccountIds = await ResolveNotifyAccountIdsAsync(order.Id, order.UserId, cancellationToken);
        var notifType = MapNotificationType(newStatus);
        var title = $"Order #{order.Id.ToString()[..8].ToUpperInvariant()} → {newStatus}";
        var message = BuildStatusMessage(newStatus);

        var createdNotifs = new List<(Guid AccountId, Notification Notif)>();
        foreach (var accountId in notifyAccountIds)
        {
            var notif = new Notification
            {
                AccountId = accountId,
                Type = notifType,
                Title = title,
                Message = message,
                IsRead = false
            };
            notif.SetCreatedAt(now);
            notif.SetUpdatedAt(now);
            _context.Notifications.Add(notif);
            createdNotifs.Add((accountId, notif));
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var (accountId, notif) in createdNotifs)
        {
            try
            {
                await _notificationService.SendNotificationToAccountAsync(
                    accountId, notif.Id, notif.Type, notif.Title, notif.Message, cancellationToken);
            }
            catch
            {
                // Realtime delivery is best-effort
            }
        }

        return Result.Success();
    }

    private async Task<HashSet<Guid>> ResolveNotifyAccountIdsAsync(
        Guid orderRequestId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid>();

        var shopkeeperAccountIds = await (
            from so in _context.SellerOrders.AsNoTracking()
            join s in _context.Shops.IgnoreQueryFilters().AsNoTracking() on so.ShopId equals s.Id
            join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where so.OrderRequestId == orderRequestId && s.DeletedAt == null
            select sk.AccountId
        ).Distinct().ToListAsync(cancellationToken);

        foreach (var id in shopkeeperAccountIds)
            recipients.Add(id);

        var customerAccountId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (customerAccountId.HasValue)
            recipients.Add(customerAccountId.Value);

        return recipients;
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

    private static NotificationType MapNotificationType(OrderStatus status) => status switch
    {
        OrderStatus.Confirmed => NotificationType.OrderConfirmed,
        OrderStatus.Shipped or OrderStatus.OutForDelivery => NotificationType.OrderShipped,
        OrderStatus.Delivered => NotificationType.OrderDelivered,
        OrderStatus.Cancelled => NotificationType.OrderCancelled,
        _ => NotificationType.OrderPlaced
    };

    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        if (to == OrderStatus.Cancelled) return from != OrderStatus.Delivered && from != OrderStatus.Refunded;
        if (from == OrderStatus.Cancelled && to != OrderStatus.Cancelled) return false;

        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Confirmed, OrderStatus.Packed) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Confirmed, OrderStatus.Delivered) => true,
            (OrderStatus.Processing, OrderStatus.Packed) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Processing, OrderStatus.Delivered) => true,
            (OrderStatus.Packed, OrderStatus.Shipped) => true,
            (OrderStatus.Packed, OrderStatus.Delivered) => true,
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

    private async Task CreditSellerEarningsForOrderAsync(
        Guid orderRequestId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sellerOrders = await (
            from so in _context.SellerOrders.AsNoTracking()
            join s in _context.Shops.IgnoreQueryFilters().AsNoTracking() on so.ShopId equals s.Id
            join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where so.OrderRequestId == orderRequestId && s.DeletedAt == null
            select new { so.Id, sk.AccountId }
        ).ToListAsync(cancellationToken);

        if (sellerOrders.Count == 0) return;

        var sellerOrderIds = sellerOrders.Select(x => x.Id).ToList();
        var earnings = await _context.SellerEarnings
            .Where(se => sellerOrderIds.Contains(se.SellerOrderId)
                      && se.Status == SellerEarningStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var earning in earnings)
        {
            earning.Status = SellerEarningStatus.Available;

            var accountId = sellerOrders.First(x => x.Id == earning.SellerOrderId).AccountId;
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);

            if (wallet is null)
            {
                wallet = new Eseller.Domain.Entities.Wallet.Wallet
                {
                    AccountId = accountId,
                    Balance = 0
                };
                wallet.SetCreatedAt(now);
                wallet.SetUpdatedAt(now);
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var credit = earning.NetAmount > 0 ? earning.NetAmount : earning.CommissionAmount;
            wallet.Balance += credit;
            wallet.SetUpdatedAt(now);

            _context.WalletTransactions.Add(new Eseller.Domain.Entities.Wallet.WalletTransaction
            {
                WalletId = wallet.Id,
                Direction = WalletDirection.Credit,
                TransactionType = WalletTransactionType.SellerEarning,
                Amount = credit,
                ReferenceType = "SellerOrder",
                ReferenceId = earning.SellerOrderId,
                Description = $"Seller earning released for delivered order",
                CreatedAt = now
            });
        }
    }

    private async Task ReversePendingSellerEarningsForOrderAsync(
        Guid orderRequestId,
        CancellationToken cancellationToken)
    {
        var sellerOrderIds = await _context.SellerOrders
            .Where(so => so.OrderRequestId == orderRequestId)
            .Select(so => so.Id)
            .ToListAsync(cancellationToken);

        if (sellerOrderIds.Count == 0) return;

        var earnings = await _context.SellerEarnings
            .Where(se => sellerOrderIds.Contains(se.SellerOrderId)
                      && se.Status == SellerEarningStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var earning in earnings)
            earning.Status = SellerEarningStatus.Withdrawn; // mark closed / not payable
    }
}
