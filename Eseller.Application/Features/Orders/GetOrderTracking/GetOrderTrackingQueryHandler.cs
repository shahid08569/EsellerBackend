using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetOrderTracking;

public class GetOrderTrackingQueryHandler
    : IRequestHandler<GetOrderTrackingQuery, Result<OrderTrackingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOrderTrackingQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<OrderTrackingDto>> Handle(
        GetOrderTrackingQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<OrderTrackingDto>("User is not authenticated.", "UNAUTHENTICATED");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<OrderTrackingDto>("Order not found.", "ORDER_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            if (role != RoleType.User)
                return Result.Failure<OrderTrackingDto>("You do not have permission.", "FORBIDDEN");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || order.UserId != user.Id)
                return Result.Failure<OrderTrackingDto>("You do not have permission.", "FORBIDDEN");
        }

        var timeline = await _context.OrderStatusHistories
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

        var dto = new OrderTrackingDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            timeline);

        return Result.Success(dto);
    }
}