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
            var partner = await _context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == accountId, cancellationToken);
            if (partner is null) return 0;

            var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken) ?? new HashSet<Guid>();

            var sellerPartnerConvos = partnerShopIds.Count > 0
                ? await _context.Conversations.AsNoTracking()
                    .Where(c => c.ParticipantType == ChatParticipantType.SellerPartner && partnerShopIds.Contains(c.ParticipantId))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken)
                : new List<Guid>();

            var partnerDirectConvos = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Partner && c.ParticipantId == partner.Id)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var allRoomIds = partnerShopIds
                .Concat(sellerPartnerConvos)
                .Concat(partnerDirectConvos)
                .Concat(new[] { partner.Id })
                .Distinct()
                .ToList();

            var unreadMsg = await _context.Messages.AsNoTracking()
                .CountAsync(m => allRoomIds.Contains(m.ConversationId)
                                 && !m.IsRead
                                 && m.SenderId != accountId,
                    cancellationToken);

            return unreadMsg;
        }

        if (role == RoleType.SuperAdmin)
        {
            var sellerPartnerConvoIds = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.SellerPartner)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var unreadMsg = await _context.Messages.AsNoTracking()
                .CountAsync(m => !sellerPartnerConvoIds.Contains(m.ConversationId)
                                 && !m.IsRead
                                 && m.SenderId != accountId
                                 && m.SenderRole != ChatSenderRole.SuperAdmin,
                    cancellationToken);

            var unreadLegacy = await _context.ChatMessages.AsNoTracking()
                .CountAsync(cm => !cm.IsRead
                                  && cm.SenderAccountId != accountId
                                  && cm.SenderRole != ChatSenderRole.SuperAdmin,
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
                .Where(c => (c.ParticipantType == ChatParticipantType.Seller || c.ParticipantType == ChatParticipantType.SellerPartner)
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
