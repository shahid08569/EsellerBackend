using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.DeleteOrder;

public class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteOrderCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only Super Admin or authorized Partner can delete orders.", "FORBIDDEN");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure("Order not found.", "ORDER_NOT_FOUND");

        if (role == RoleType.Partner)
        {
            var hasPerm = await PartnerPermissionChecker.HasPermissionAsync(
                _context, _currentUserService, "orders:manage", cancellationToken);
            if (!hasPerm)
                return Result.Failure("You do not have permission to delete orders.", "FORBIDDEN");

            var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            var isPartnerOrder = await _context.SellerOrders
                .AnyAsync(so => so.OrderRequestId == order.Id && partnerShopIds != null && partnerShopIds.Contains(so.ShopId), cancellationToken);

            if (!isPartnerOrder)
                return Result.Failure("Partners can only manage orders for their referred shops.", "FORBIDDEN");
        }

        // 1. Delete Return Requests, Return Items, and Return Refunds for SellerOrders
        var sellerOrders = await _context.SellerOrders
            .Where(s => s.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);

        if (sellerOrders.Count > 0)
        {
            var sellerOrderIds = sellerOrders.Select(s => s.Id).ToList();

            var returnRequests = await _context.ReturnRequests
                .Where(r => sellerOrderIds.Contains(r.SellerOrderId))
                .ToListAsync(cancellationToken);
            if (returnRequests.Count > 0)
            {
                var returnRequestIds = returnRequests.Select(r => r.Id).ToList();
                var returnItems = await _context.ReturnItems
                    .Where(ri => returnRequestIds.Contains(ri.ReturnRequestId))
                    .ToListAsync(cancellationToken);
                if (returnItems.Count > 0)
                {
                    _context.ReturnItems.RemoveRange(returnItems);
                }

                var returnRefunds = await _context.Refunds
                    .Where(rf => rf.ReturnRequestId != null && returnRequestIds.Contains(rf.ReturnRequestId.Value))
                    .ToListAsync(cancellationToken);
                if (returnRefunds.Count > 0)
                {
                    _context.Refunds.RemoveRange(returnRefunds);
                }

                _context.ReturnRequests.RemoveRange(returnRequests);
            }

            // Shipments
            var shipments = await _context.Shipments
                .Where(s => sellerOrderIds.Contains(s.SellerOrderId))
                .ToListAsync(cancellationToken);
            if (shipments.Count > 0)
            {
                _context.Shipments.RemoveRange(shipments);
            }

            // SellerEarnings and wallet adjustment
            var earnings = await _context.SellerEarnings
                .Where(e => sellerOrderIds.Contains(e.SellerOrderId))
                .ToListAsync(cancellationToken);
            if (earnings.Count > 0)
            {
                await SellerEarningsWalletEffects.UndoWalletBeforeDeleteAsync(
                    _context, _dateTime, earnings, sellerOrderIds, cancellationToken);
                _context.SellerEarnings.RemoveRange(earnings);
            }
        }

        // 2. Delete Affiliate Commissions
        var affiliateCommissions = await _context.AffiliateCommissions
            .Where(ac => ac.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (affiliateCommissions.Count > 0)
        {
            _context.AffiliateCommissions.RemoveRange(affiliateCommissions);
        }

        // 3. Delete Coupon Usages
        var couponUsages = await _context.CouponUsages
            .Where(cu => cu.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (couponUsages.Count > 0)
        {
            _context.CouponUsages.RemoveRange(couponUsages);
        }

        // 4. Delete Payments, Payment Attempts, and direct Payment Refunds
        var payments = await _context.Payments
            .Where(p => p.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (payments.Count > 0)
        {
            var paymentIds = payments.Select(p => p.Id).ToList();
            var paymentRefunds = await _context.Refunds
                .Where(rf => rf.PaymentId != null && paymentIds.Contains(rf.PaymentId.Value))
                .ToListAsync(cancellationToken);
            if (paymentRefunds.Count > 0)
            {
                _context.Refunds.RemoveRange(paymentRefunds);
            }

            var paymentAttempts = await _context.PaymentAttempts
                .Where(pa => paymentIds.Contains(pa.PaymentId))
                .ToListAsync(cancellationToken);
            if (paymentAttempts.Count > 0)
            {
                _context.PaymentAttempts.RemoveRange(paymentAttempts);
            }

            _context.Payments.RemoveRange(payments);
        }

        // 5. Delete OrderRequestItems
        var items = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            _context.OrderRequestItems.RemoveRange(items);
        }

        // 6. Delete SellerOrders
        if (sellerOrders.Count > 0)
        {
            _context.SellerOrders.RemoveRange(sellerOrders);
        }

        // 7. Delete OrderStatusHistories
        var history = await _context.OrderStatusHistories
            .Where(h => h.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (history.Count > 0)
        {
            _context.OrderStatusHistories.RemoveRange(history);
        }

        // 8. Delete ChatMessages
        var chatMessages = await _context.ChatMessages
            .Where(m => m.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (chatMessages.Count > 0)
        {
            _context.ChatMessages.RemoveRange(chatMessages);
        }

        // 9. Delete Related Notifications
        var notifications = await _context.Notifications
            .Where(n => n.EntityId == order.Id)
            .ToListAsync(cancellationToken);
        if (notifications.Count > 0)
        {
            _context.Notifications.RemoveRange(notifications);
        }

        // 10. Delete Inventory Transactions referencing order
        var inventoryTx = await _context.InventoryTransactions
            .Where(it => it.ReferenceId == order.Id)
            .ToListAsync(cancellationToken);
        if (inventoryTx.Count > 0)
        {
            _context.InventoryTransactions.RemoveRange(inventoryTx);
        }

        // 11. Delete OrderRequest
        _context.OrderRequests.Remove(order);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
