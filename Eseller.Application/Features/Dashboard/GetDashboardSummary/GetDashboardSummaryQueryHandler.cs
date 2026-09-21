using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.GetDashboardSummary;

public class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDashboardSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<DashboardSummaryDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure<DashboardSummaryDto>(
                "Only customers can access dashboard.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<DashboardSummaryDto>(
                "User not found.", "USER_NOT_FOUND");

        // Orders
        var orders = await _context.OrderRequests
            .Where(o => o.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();

        // Order items for total spent
        var orderItems = await _context.OrderRequestItems
            .Where(i => orderIds.Contains(i.OrderRequestId))
            .ToListAsync(cancellationToken);

        // Wishlist
        var wishlistCount = await _context.Wishlists
            .CountAsync(w => w.UserId == user.Id, cancellationToken);

        // Compare
        var compareCount = await _context.CompareItems
            .CountAsync(c => c.UserId == user.Id, cancellationToken);

        // Addresses
        var addressCount = await _context.Addresses
            .CountAsync(a => a.UserId == user.Id && a.IsActive, cancellationToken);

        // Notifications
        var unreadNotifications = await _context.Notifications
            .CountAsync(n => n.AccountId == _currentUserService.AccountId.Value && !n.IsRead,
                cancellationToken);

        // Chat messages
        var unreadChatMessages = await _context.ChatMessages
            .CountAsync(cm => orderIds.Contains(cm.OrderRequestId)
                           && !cm.IsRead
                           && cm.SenderRole != ChatSenderRole.User,
                cancellationToken);

        var totalSpent = orderItems.Sum(i => i.TotalPriceSnapshot);

        var dto = new DashboardSummaryDto(
            orders.Count,
            orders.Count(o => o.Status == OrderStatus.Pending),
            orders.Count(o => o.Status == OrderStatus.Delivered),
            orders.Count(o => o.Status == OrderStatus.Cancelled),
            wishlistCount,
            compareCount,
            addressCount,
            unreadNotifications,
            unreadChatMessages,
            totalSpent);

        return Result.Success(dto);
    }
}