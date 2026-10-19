using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetOrderStatusHistory;

public class GetOrderStatusHistoryQueryHandler
    : IRequestHandler<GetOrderStatusHistoryQuery, Result<IReadOnlyList<OrderStatusHistoryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOrderStatusHistoryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<OrderStatusHistoryDto>>> Handle(
        GetOrderStatusHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<OrderStatusHistoryDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<IReadOnlyList<OrderStatusHistoryDto>>(
                "Order not found.", "ORDER_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (role == RoleType.Partner)
        {
            var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            var isPartnerOrder = await _context.SellerOrders
                .AnyAsync(so => so.OrderRequestId == order.Id && partnerShopIds != null && partnerShopIds.Contains(so.ShopId), cancellationToken);

            if (!isPartnerOrder)
                return Result.Failure<IReadOnlyList<OrderStatusHistoryDto>>("Partners can only view order history for their referred shops.", "FORBIDDEN");
        }

        if (!isAdmin)
        {
            if (role != RoleType.User)
                return Result.Failure<IReadOnlyList<OrderStatusHistoryDto>>(
                    "You do not have permission.", "FORBIDDEN");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || order.UserId != user.Id)
                return Result.Failure<IReadOnlyList<OrderStatusHistoryDto>>(
                    "You do not have permission.", "FORBIDDEN");
        }

        var history = await _context.OrderStatusHistories
            .Where(h => h.OrderRequestId == request.OrderId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new OrderStatusHistoryDto(
                h.Id,
                h.OrderRequestId,
                h.OldStatus.ToString(),
                h.NewStatus.ToString(),
                h.ChangedByAccountId,
                h.ChangedByType,
                h.ChangedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<OrderStatusHistoryDto>>(history);
    }
}