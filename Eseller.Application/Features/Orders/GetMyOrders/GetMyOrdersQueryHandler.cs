using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetMyOrders;

public class GetMyOrdersQueryHandler
    : IRequestHandler<GetMyOrdersQuery, Result<PagedList<OrderRequestListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyOrdersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<OrderRequestListDto>>> Handle(
        GetMyOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<OrderRequestListDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<PagedList<OrderRequestListDto>>(
                "Only customers can view orders.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<PagedList<OrderRequestListDto>>(
                "User not found.", "USER_NOT_FOUND");

        var query = _context.OrderRequests
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var orderList = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orderIds = orderList.Select(o => o.Id).ToList();

        // Load order items
        var items = await _context.OrderRequestItems
            .Where(i => orderIds.Contains(i.OrderRequestId))
            .ToListAsync(cancellationToken);

        var itemsByOrder = items
            .GroupBy(i => i.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load seller orders
        var sellerOrders = await _context.SellerOrders
            .Where(so => orderIds.Contains(so.OrderRequestId))
            .ToListAsync(cancellationToken);

        var shopsByOrder = sellerOrders
            .GroupBy(so => so.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.Select(so => so.ShopId).Distinct().Count());

        var result = orderList.Select(o =>
        {
            var orderItems = itemsByOrder.GetValueOrDefault(o.Id) ?? Enumerable.Empty<Domain.Entities.Orders.OrderRequestItem>().ToList();

            return new OrderRequestListDto(
                o.Id,
                o.Status.ToString(),
                orderItems.Sum(i => i.TotalPriceSnapshot),
                orderItems.Sum(i => i.Quantity),
                shopsByOrder.GetValueOrDefault(o.Id, 0),
                o.CreatedAt);
        }).ToList();

        var pagedList = new PagedList<OrderRequestListDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}