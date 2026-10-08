using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Wallet;
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
    private readonly IAppNotificationService _notifications;

    public UpdateSellerOrderStatusCommandHandler(
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

        var now = _dateTime.UtcNow;
        sellerOrder.Status = newStatus;
        sellerOrder.SetUpdatedAt(now);

        if (newStatus == OrderStatus.Cancelled)
        {
            await SellerEarningsWalletEffects.ReversePendingAsync(
                _context, _dateTime, [sellerOrder.Id], cancellationToken);
        }

        if (newStatus == OrderStatus.Delivered)
        {
            var earning = await _context.SellerEarnings
                .FirstOrDefaultAsync(se => se.SellerOrderId == sellerOrder.Id
                                        && se.Status == SellerEarningStatus.Pending, cancellationToken);
            Guid? creditedAccountId = null;
            decimal creditedAmount = 0;
            if (earning != null)
            {
                earning.Status = SellerEarningStatus.Available;

                var shop = await _context.Shops
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == sellerOrder.ShopId, cancellationToken);
                var keeper = shop is null
                    ? null
                    : await _context.Shopkeepers
                        .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

                if (keeper is not null)
                {
                    var wallet = await _context.Wallets
                        .FirstOrDefaultAsync(w => w.AccountId == keeper.AccountId, cancellationToken);
                    if (wallet is null)
                    {
                        wallet = new Eseller.Domain.Entities.Wallet.Wallet
                        {
                            AccountId = keeper.AccountId,
                            Balance = 0,
                            PendingEarnings = 0,
                            AvailableEarnings = 0
                        };
                        wallet.SetCreatedAt(now);
                        wallet.SetUpdatedAt(now);
                        _context.Wallets.Add(wallet);
                    }

                    var credit = earning.NetAmount > 0 ? earning.NetAmount : earning.CommissionAmount;
                    if (credit < 0) credit = 0;
                    wallet.Balance += credit;
                    wallet.PendingEarnings = Math.Max(0m, wallet.PendingEarnings - credit);
                    wallet.AvailableEarnings += credit;
                    wallet.SetUpdatedAt(now);

                    _context.WalletTransactions.Add(new Eseller.Domain.Entities.Wallet.WalletTransaction
                    {
                        WalletId = wallet.Id,
                        Direction = WalletDirection.Credit,
                        TransactionType = WalletTransactionType.SellerEarning,
                        Amount = credit,
                        ReferenceType = "SellerOrder",
                        ReferenceId = earning.SellerOrderId,
                        Description = "Seller earning released for delivered order",
                        CreatedAt = now
                    });

                    creditedAccountId = keeper.AccountId;
                    creditedAmount = credit;
                }
            }

            // Keep parent OrderRequest in sync when this is the only shop,
            // or when every sibling SellerOrder already matches the new status.
            var otherSellerOrders = await _context.SellerOrders
                .Where(so => so.OrderRequestId == sellerOrder.OrderRequestId && so.Id != sellerOrder.Id)
                .ToListAsync(cancellationToken);

            if (otherSellerOrders.Count == 0 || otherSellerOrders.All(so => so.Status == newStatus))
            {
                var mainOrder = await _context.OrderRequests
                    .FirstOrDefaultAsync(o => o.Id == sellerOrder.OrderRequestId, cancellationToken);
                if (mainOrder != null && mainOrder.Status != newStatus)
                {
                    mainOrder.Status = newStatus;
                    mainOrder.SetUpdatedAt(now);
                }
            }

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
                ChangedAt = now
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

            try
            {
                var orderRef = sellerOrder.OrderRequestId.ToString()[..8].ToUpperInvariant();
                var sellerAccount = await (
                    from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
                    join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
                    where s.Id == sellerOrder.ShopId
                    select sk.AccountId
                ).FirstOrDefaultAsync(cancellationToken);

                if (sellerAccount != Guid.Empty && oldStatus != OrderStatus.Delivered)
                {
                    await _notifications.NotifyAsync(
                        sellerAccount,
                        RoleType.Shopkeeper,
                        NotificationType.OrderDelivered,
                        $"Order #{orderRef} delivered",
                        "An order has been marked as delivered.",
                        NotificationEntityType.Order,
                        sellerOrder.OrderRequestId,
                        "/orders",
                        $"OrderDelivered:{sellerOrder.Id:N}:Seller:{sellerAccount:N}",
                        cancellationToken);
                }

                if (creditedAccountId.HasValue && creditedAmount > 0)
                {
                    await _notifications.NotifyAsync(
                        creditedAccountId.Value,
                        RoleType.Shopkeeper,
                        NotificationType.PaymentCompleted,
                        "Payment Completed",
                        $"Payment for order #{orderRef} completed. Rs. {creditedAmount:N0} credited.",
                        NotificationEntityType.Payment,
                        sellerOrder.OrderRequestId,
                        "/earnings",
                        $"PaymentCompleted:{sellerOrder.Id:N}:Seller:{creditedAccountId.Value:N}",
                        cancellationToken);

                    await _notifications.NotifyAsync(
                        creditedAccountId.Value,
                        RoleType.Shopkeeper,
                        NotificationType.CommissionEarned,
                        "Commission Earned",
                        $"Congratulations! You earned Rs. {creditedAmount:N0} from order #{orderRef}.",
                        NotificationEntityType.Earning,
                        sellerOrder.OrderRequestId,
                        "/earnings",
                        $"CommissionEarned:{sellerOrder.Id:N}:Seller:{creditedAccountId.Value:N}",
                        cancellationToken);
                }
            }
            catch
            {
                // Status already saved — notifications must not fail delivery.
            }

            return Result.Success();
        }

        // Keep parent OrderRequest in sync when this is the only shop,
        // or when every sibling SellerOrder already matches the new status.
        var otherOrders = await _context.SellerOrders
            .Where(so => so.OrderRequestId == sellerOrder.OrderRequestId && so.Id != sellerOrder.Id)
            .ToListAsync(cancellationToken);

        if (otherOrders.Count == 0 || otherOrders.All(so => so.Status == newStatus))
        {
            var mainOrder = await _context.OrderRequests
                .FirstOrDefaultAsync(o => o.Id == sellerOrder.OrderRequestId, cancellationToken);
            if (mainOrder != null && mainOrder.Status != newStatus)
            {
                mainOrder.Status = newStatus;
                mainOrder.SetUpdatedAt(now);
            }
        }

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
            ChangedAt = now
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
            (OrderStatus.Processing, OrderStatus.OnTheWay) => true,
            (OrderStatus.OnTheWay, OrderStatus.Shipped) => true,
            (OrderStatus.OnTheWay, OrderStatus.OutForDelivery) => true,
            (OrderStatus.OnTheWay, OrderStatus.Delivered) => true,
            (OrderStatus.Shipped, OrderStatus.OutForDelivery) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Delivered) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            _ => false
        };
    }
}