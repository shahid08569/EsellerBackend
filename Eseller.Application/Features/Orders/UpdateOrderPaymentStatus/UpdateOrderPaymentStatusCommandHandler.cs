using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.UpdateOrderPaymentStatus;

public class UpdateOrderPaymentStatusCommandHandler
    : IRequestHandler<UpdateOrderPaymentStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _dateTime;

    public UpdateOrderPaymentStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTime dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateOrderPaymentStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUser.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only Super Admin can update payment status.", "FORBIDDEN");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure("Order not found.", "ORDER_NOT_FOUND");

        if (!string.IsNullOrWhiteSpace(order.ShippingAddress)
            && (order.ShippingAddress.StartsWith("CUSTOMER_SUPPORT_", StringComparison.OrdinalIgnoreCase)
                || order.ShippingAddress.StartsWith("SHOP_", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure("This is a chat channel, not a commerce order.", "NOT_A_COMMERCE_ORDER");
        }

        var payment = await _context.Payments
            .Where(p => p.OrderRequestId == order.Id)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _dateTime.UtcNow;
        var oldStatus = payment?.Status;

        if (payment is null)
        {
            var amount = await _context.OrderRequestItems
                .Where(i => i.OrderRequestId == order.Id)
                .SumAsync(i => i.TotalPriceSnapshot, cancellationToken);

            payment = new Domain.Entities.Payment.Payment
            {
                OrderRequestId = order.Id,
                Method = PaymentMethod.CashOnDelivery,
                Currency = "USD",
                Amount = amount,
                Status = request.NewStatus
            };
            payment.SetCreatedAt(now);
            payment.SetUpdatedAt(now);
            _context.Payments.Add(payment);
        }
        else
        {
            if (payment.Status == request.NewStatus)
                return Result.Failure("Payment is already in this status.", "NO_CHANGE");

            payment.Status = request.NewStatus;
            payment.SetUpdatedAt(now);
        }

        // When payment becomes Completed (Paid), release pending earnings for any delivered seller orders.
        if (request.NewStatus == PaymentStatus.Completed && oldStatus != PaymentStatus.Completed)
        {
            var deliveredSellerOrderIds = await _context.SellerOrders
                .Where(so => so.OrderRequestId == order.Id && so.Status == OrderStatus.Delivered)
                .Select(so => so.Id)
                .ToListAsync(cancellationToken);

            if (deliveredSellerOrderIds.Count > 0)
            {
                await SellerEarningsWalletEffects.ReleasePendingToAvailableAsync(
                    _context, _dateTime, order.Id, cancellationToken, deliveredSellerOrderIds);
            }
        }
        // If payment was reverted from Completed (Paid) to unpaid (Pending, Failed, etc.), move available earnings back to pending.
        else if (oldStatus == PaymentStatus.Completed && request.NewStatus != PaymentStatus.Completed)
        {
            await SellerEarningsWalletEffects.RevertAvailableToPendingAsync(
                _context, _dateTime, order.Id, cancellationToken);
        }

        _context.AuditLogs.Add(new Eseller.Domain.Entities.Identity.AuditLog
        {
            Id = Guid.NewGuid(),
            ActorAccountId = _currentUser.AccountId,
            ActorType = _currentUser.RoleType.ToString(),
            Action = "UPDATE_ORDER_PAYMENT_STATUS",
            EntityType = "OrderPayment",
            EntityId = order.Id,
            OldValues = $"Status: {oldStatus?.ToString() ?? "None"}",
            NewValues = $"Status: {request.NewStatus}",
            IpAddress = null,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
