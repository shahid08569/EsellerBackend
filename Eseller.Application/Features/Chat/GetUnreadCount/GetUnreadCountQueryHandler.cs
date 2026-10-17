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
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(0);
        }
        catch (OperationCanceledException)
        {
            return Result.Success(0);
        }
    }

    private async Task<int> CountUnreadAsync(CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;
        if (_currentUserService.AccountId is null) return 0;
        var accountId = _currentUserService.AccountId.Value;

        if (role == RoleType.Partner)
        {
            var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            if (partnerShopIds == null || partnerShopIds.Count == 0)
                return 0;

            var convoIds = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Seller
                            && partnerShopIds.Contains(c.ParticipantId))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var allRoomIds = partnerShopIds.Concat(convoIds).Distinct().ToList();

            var unreadMsg = await _context.Messages.AsNoTracking()
                .CountAsync(m => allRoomIds.Contains(m.ConversationId)
                                 && !m.IsRead
                                 && m.SenderId != accountId
                                 && m.SenderRole != ChatSenderRole.SuperAdmin
                                 && m.SenderRole != ChatSenderRole.Partner,
                    cancellationToken);

            var unreadLegacy = await _context.ChatMessages.AsNoTracking()
                .CountAsync(cm => allRoomIds.Contains(cm.OrderRequestId)
                                  && !cm.IsRead
                                  && cm.SenderAccountId != accountId
                                  && cm.SenderRole != ChatSenderRole.SuperAdmin
                                  && cm.SenderRole != ChatSenderRole.Partner,
                    cancellationToken);

            return unreadMsg + unreadLegacy;
        }

        if (role == RoleType.SuperAdmin)
        {
            var unreadMsg = await _context.Messages.AsNoTracking()
                .CountAsync(m => !m.IsRead
                                 && m.SenderId != accountId
                                 && m.SenderRole != ChatSenderRole.SuperAdmin
                                 && m.SenderRole != ChatSenderRole.Partner,
                    cancellationToken);

            var unreadLegacy = await _context.ChatMessages.AsNoTracking()
                .CountAsync(cm => !cm.IsRead
                                  && cm.SenderAccountId != accountId
                                  && cm.SenderRole != ChatSenderRole.SuperAdmin
                                  && cm.SenderRole != ChatSenderRole.Partner,
                    cancellationToken);

            return unreadMsg + unreadLegacy;
        }

        if (role == RoleType.Shopkeeper)
        {
            var shopkeeper = await _context.Shopkeepers.AsNoTracking()
                .FirstOrDefaultAsync(sk => sk.AccountId == accountId,
                    cancellationToken);
            if (shopkeeper is null) return 0;

            var shopIds = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (shopIds.Count == 0) return 0;

            var convoIds = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Seller
                            && shopIds.Contains(c.ParticipantId))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var allRoomIds = shopIds.Concat(convoIds).Distinct().ToList();

            if (allRoomIds.Count == 0) return 0;

            var unreadMsg = await _context.Messages.AsNoTracking()
                .CountAsync(m => allRoomIds.Contains(m.ConversationId)
                                 && !m.IsRead
                                 && m.SenderId != accountId
                                 && m.SenderRole != ChatSenderRole.Shopkeeper,
                    cancellationToken);

            var unreadLegacy = await _context.ChatMessages.AsNoTracking()
                .CountAsync(cm => allRoomIds.Contains(cm.OrderRequestId)
                                  && !cm.IsRead
                                  && cm.SenderAccountId != accountId
                                  && cm.SenderRole != ChatSenderRole.Shopkeeper,
                    cancellationToken);

            return unreadMsg + unreadLegacy;
        }

        if (role == RoleType.User)
        {
            var userId = await _context.Users.AsNoTracking()
                .Where(u => u.AccountId == accountId)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (userId is null) return 0;

            var convoIds = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Customer
                            && c.ParticipantId == userId.Value)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var userOrders = await _context.OrderRequests.AsNoTracking()
                .Where(o => o.UserId == userId.Value)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var allRoomIds = convoIds.Concat(userOrders).Concat(new[] { userId.Value }).Distinct().ToList();

            var unreadMsg = await _context.Messages.AsNoTracking()
                .CountAsync(m => allRoomIds.Contains(m.ConversationId)
                                 && !m.IsRead
                                 && m.SenderId != accountId
                                 && m.SenderRole != ChatSenderRole.User,
                    cancellationToken);

            var unreadLegacy = await _context.ChatMessages.AsNoTracking()
                .CountAsync(cm => allRoomIds.Contains(cm.OrderRequestId)
                                  && !cm.IsRead
                                  && cm.SenderAccountId != accountId
                                  && cm.SenderRole != ChatSenderRole.User,
                    cancellationToken);

            return unreadMsg + unreadLegacy;
        }

        return 0;
    }
}
