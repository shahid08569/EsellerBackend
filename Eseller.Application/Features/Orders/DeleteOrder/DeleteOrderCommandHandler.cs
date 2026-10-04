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

    public DeleteOrderCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        // 1. Delete OrderRequestItems
        var items = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            _context.OrderRequestItems.RemoveRange(items);
        }

        // 2. Delete SellerOrders and related SellerOrderItems & SellerEarnings
        var sellerOrders = await _context.SellerOrders
            .Where(s => s.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);

        if (sellerOrders.Count > 0)
        {
            var sellerOrderIds = sellerOrders.Select(s => s.Id).ToList();

            var earnings = await _context.SellerEarnings
                .Where(e => sellerOrderIds.Contains(e.SellerOrderId))
                .ToListAsync(cancellationToken);
            if (earnings.Count > 0)
            {
                _context.SellerEarnings.RemoveRange(earnings);
            }

            _context.SellerOrders.RemoveRange(sellerOrders);
        }

        // 3. Delete OrderStatusHistories
        var history = await _context.OrderStatusHistories
            .Where(h => h.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (history.Count > 0)
        {
            _context.OrderStatusHistories.RemoveRange(history);
        }

        // 4. Delete ChatMessages
        var chatMessages = await _context.ChatMessages
            .Where(m => m.OrderRequestId == order.Id)
            .ToListAsync(cancellationToken);
        if (chatMessages.Count > 0)
        {
            _context.ChatMessages.RemoveRange(chatMessages);
        }

        // 5. Delete OrderRequest
        _context.OrderRequests.Remove(order);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
