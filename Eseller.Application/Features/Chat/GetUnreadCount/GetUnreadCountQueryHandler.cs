using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.GetUnreadCount;

public class GetUnreadCountQueryHandler
    : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(
        GetUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<int>("User is not authenticated.", "UNAUTHENTICATED");

        try
        {
            return Result.Success(await CountUnreadAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client navigated away / poll aborted — not a server fault
            return Result.Success(0);
        }
    }

    private async Task<int> CountUnreadAsync(CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;

        if (role == RoleType.SuperAdmin || role == RoleType.Partner)
        {
            return await _context.ChatMessages
                .AsNoTracking()
                .CountAsync(cm => !cm.IsRead
                    && (cm.SenderRole == ChatSenderRole.User
                        || cm.SenderRole == ChatSenderRole.Shopkeeper),
                    cancellationToken);
        }

        if (role == RoleType.Shopkeeper)
        {
            var shopkeeper = await _context.Shopkeepers
                .AsNoTracking()
                .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId!.Value,
                    cancellationToken);

            if (shopkeeper is null)
                return 0;

            var shopIds = await _context.Shops
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (shopIds.Count == 0)
                return 0;

            // 1) Super Admin → seller support rooms (OrderRequestId == Shop.Id)
            var supportUnread = await _context.ChatMessages
                .AsNoTracking()
                .CountAsync(cm => shopIds.Contains(cm.OrderRequestId)
                               && !cm.IsRead
                               && (cm.SenderRole == ChatSenderRole.SuperAdmin
                                   || cm.SenderRole == ChatSenderRole.Partner),
                    cancellationToken);

            // 2) Customer ↔ shop threads (SHOP_CHAT_{shopId}_*) — filter in SQL per shop
            var customerUnread = 0;
            foreach (var shopId in shopIds)
            {
                var prefix = $"SHOP_CHAT_{shopId}_";
                customerUnread += await (
                    from cm in _context.ChatMessages.AsNoTracking()
                    join o in _context.OrderRequests.AsNoTracking() on cm.OrderRequestId equals o.Id
                    where !cm.IsRead
                          && cm.SenderRole == ChatSenderRole.User
                          && o.ShippingAddress != null
                          && o.ShippingAddress.StartsWith(prefix)
                    select cm.Id
                ).CountAsync(cancellationToken);
            }

            // 3) Commerce order chats for this seller (join — no giant Id list in memory)
            var orderUnread = await (
                from cm in _context.ChatMessages.AsNoTracking()
                join so in _context.SellerOrders.AsNoTracking() on cm.OrderRequestId equals so.OrderRequestId
                where shopIds.Contains(so.ShopId)
                      && !cm.IsRead
                      && cm.SenderRole == ChatSenderRole.User
                      && !shopIds.Contains(cm.OrderRequestId)
                select cm.Id
            ).Distinct().CountAsync(cancellationToken);

            return supportUnread + customerUnread + orderUnread;
        }

        if (role == RoleType.User)
        {
            var userId = await _context.Users
                .AsNoTracking()
                .Where(u => u.AccountId == _currentUserService.AccountId!.Value)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (userId is null)
                return 0;

            // Single SQL join — avoid loading all order IDs then IN-clause
            return await (
                from cm in _context.ChatMessages.AsNoTracking()
                join o in _context.OrderRequests.AsNoTracking() on cm.OrderRequestId equals o.Id
                where o.UserId == userId.Value
                      && !cm.IsRead
                      && cm.SenderRole != ChatSenderRole.User
                select cm.Id
            ).CountAsync(cancellationToken);
        }

        return 0;
    }
}
