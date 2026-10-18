using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.GetChatMessages;

public class GetChatMessagesQueryHandler
    : IRequestHandler<GetChatMessagesQuery, Result<PagedList<ChatMessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public GetChatMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService signalR)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _signalR = signalR;
    }

    public async Task<Result<PagedList<ChatMessageDto>>> Handle(
        GetChatMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.OrderRequestId, cancellationToken);

        if (conversation is null)
        {
            var targetType = role == RoleType.Partner ? ChatParticipantType.SellerPartner : ChatParticipantType.Seller;
            conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ParticipantId == request.OrderRequestId && c.ParticipantType == targetType, cancellationToken);
        }

        if (conversation is null)
        {
            // Lazy ensure shop/user rooms
            var shop = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(s => (s.Id == request.OrderRequestId || s.ShopkeeperId == request.OrderRequestId) && s.DeletedAt == null, cancellationToken);

            if (shop is not null)
            {
                var targetType = role == RoleType.Partner ? ChatParticipantType.SellerPartner : ChatParticipantType.Seller;
                conversation = await SupportConversationService.GetOrCreateAsync(
                    _context, shop.Id, targetType,
                    _dateTime.UtcNow, cancellationToken);
            }
            else
            {
                var partner = await _context.Partners.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == request.OrderRequestId || p.AccountId == request.OrderRequestId, cancellationToken);

                if (partner is null && role == RoleType.Partner && _currentUserService.AccountId.HasValue)
                {
                    var myPartner = await _context.Partners.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.AccountId == _currentUserService.AccountId.Value, cancellationToken);
                    if (myPartner != null && (request.OrderRequestId == myPartner.Id || request.OrderRequestId == myPartner.AccountId))
                    {
                        partner = myPartner;
                    }
                }

                if (partner is not null)
                {
                    conversation = await SupportConversationService.GetOrCreateAsync(
                        _context, partner.Id, ChatParticipantType.Partner,
                        _dateTime.UtcNow, cancellationToken);
                }
                else
                {
                    var isUser = await _context.Users.AsNoTracking()
                        .AnyAsync(u => u.Id == request.OrderRequestId, cancellationToken);
                if (isUser)
                {
                    conversation = await SupportConversationService.GetOrCreateAsync(
                        _context, request.OrderRequestId, ChatParticipantType.Customer,
                        _dateTime.UtcNow, cancellationToken);
                }
                else
                {
                    var order = await _context.OrderRequests.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == request.OrderRequestId, cancellationToken);
                    if (order is not null && order.UserId != Guid.Empty)
                    {
                        conversation = await SupportConversationService.GetOrCreateAsync(
                            _context, order.UserId, ChatParticipantType.Customer,
                            _dateTime.UtcNow, cancellationToken);
                    }
                }
            }
        }
    }

        if (conversation is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "Conversation not found.", "ORDER_NOT_FOUND");

        if (!await SupportConversationService.CanAccessAsync(
                _context, _currentUserService, conversation, cancellationToken))
            return Result.Failure<PagedList<ChatMessageDto>>(
                "You do not have permission.", "FORBIDDEN");

        List<Guid> roomIds;

        if (conversation.ParticipantType == ChatParticipantType.SellerPartner)
        {
            roomIds = new List<Guid> { conversation.Id };
            if (conversation.ParticipantId != Guid.Empty && conversation.ParticipantId != conversation.Id)
            {
                roomIds.Add(conversation.ParticipantId);
            }
        }
        else if (conversation.ParticipantType == ChatParticipantType.Partner)
        {
            roomIds = new List<Guid> { conversation.Id };
            if (conversation.ParticipantId != Guid.Empty && conversation.ParticipantId != conversation.Id)
            {
                roomIds.Add(conversation.ParticipantId);
            }
        }
        else if (conversation.ParticipantType == ChatParticipantType.Customer)
        {
            roomIds = new[] { conversation.Id, conversation.ParticipantId, request.OrderRequestId }
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var userOrders = await _context.OrderRequests.AsNoTracking()
                .Where(o => o.UserId == conversation.ParticipantId)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            roomIds.AddRange(userOrders);
            roomIds = roomIds.Distinct().ToList();
        }
        else if (conversation.ParticipantType == ChatParticipantType.Seller)
        {
            roomIds = new[] { conversation.Id, conversation.ParticipantId, request.OrderRequestId }
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var shopOrderReqIds = await _context.SellerOrders.AsNoTracking()
                .Where(so => so.ShopId == conversation.ParticipantId)
                .Select(so => so.OrderRequestId)
                .ToListAsync(cancellationToken);
            roomIds.AddRange(shopOrderReqIds);

            // ONLY include other conversations with the EXACT SAME ParticipantType (Seller)! NEVER SellerPartner!
            var otherSellerConvos = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantId == conversation.ParticipantId && c.ParticipantType == ChatParticipantType.Seller)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            roomIds.AddRange(otherSellerConvos);
            roomIds = roomIds.Distinct().ToList();
        }
        else
        {
            roomIds = new List<Guid> { conversation.Id };
        }

        var accountId = _currentUserService.AccountId.Value;
        var accountIdStr = accountId.ToString();

        // Automatically mark unread inbound messages as read when opening conversation (unless SuperAdmin read-only audit)
        var isReadOnlyAudit = role == RoleType.SuperAdmin && conversation.ParticipantType == ChatParticipantType.SellerPartner;
        if (!isReadOnlyAudit)
        {
            var unreadEntities = await _context.Messages
                .Where(m => roomIds.Contains(m.ConversationId)
                            && !m.IsRead
                            && m.SenderId != accountId)
                .ToListAsync(cancellationToken);

            if (unreadEntities.Count > 0)
            {
                var now = _dateTime.UtcNow;
                foreach (var msg in unreadEntities)
                {
                    msg.IsRead = true;
                    msg.ReadAt = now;
                    if (msg.DeliveredAt == null)
                    {
                        msg.DeliveredAt = now;
                    }
                }
                await _context.SaveChangesAsync(cancellationToken);

                try
                {
                    foreach (var msg in unreadEntities)
                    {
                        await _signalR.BroadcastMessageReadAsync(
                            conversation.Id,
                            msg.Id,
                            now,
                            cancellationToken,
                            notifyAccountIds: new[] { msg.SenderId });

                        if (conversation.ParticipantId != Guid.Empty && conversation.ParticipantId != conversation.Id)
                        {
                            await _signalR.BroadcastMessageReadAsync(
                                conversation.ParticipantId,
                                msg.Id,
                                now,
                                cancellationToken,
                                notifyAccountIds: new[] { msg.SenderId });
                        }
                    }
                }
                catch
                {
                    // best-effort realtime
                }
            }
        }

        // 1. Modern Messages table
        var modernQuery = _context.Messages
            .AsNoTracking()
            .Where(m => roomIds.Contains(m.ConversationId)
                && (m.DeletedForAccountIds == null || !m.DeletedForAccountIds.Contains(accountIdStr)));

        if (conversation.ParticipantType == ChatParticipantType.SellerPartner)
        {
            modernQuery = modernQuery.Where(m => m.SenderRole != ChatSenderRole.SuperAdmin);
        }
        else if (conversation.ParticipantType == ChatParticipantType.Seller)
        {
            modernQuery = modernQuery.Where(m => m.SenderRole != ChatSenderRole.Partner);
        }

        var modernList = await modernQuery
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ConversationId,
                m.SenderId,
                m.SenderRole.ToString(),
                m.IsDeletedForEveryone ? "This message was deleted" : m.Content,
                m.IsRead,
                m.ReadAt,
                m.DeliveredAt ?? (m.IsRead ? (m.ReadAt ?? m.SentAt) : null),
                m.SentAt,
                m.IsDeletedForEveryone ? null : m.AttachmentUrl,
                m.IsDeletedForEveryone ? null : m.AttachmentFileName,
                m.IsDeletedForEveryone ? null : m.AttachmentContentType,
                m.IsDeletedForEveryone ? null : m.AttachmentSizeBytes,
                m.IsEdited,
                m.EditedAt,
                m.IsDeletedForEveryone))
            .ToListAsync(cancellationToken);

        // 2. Legacy ChatMessages table
        var legacyQuery = _context.ChatMessages
            .AsNoTracking()
            .Where(cm => roomIds.Contains(cm.OrderRequestId)
                && (cm.DeletedForAccountIds == null || !cm.DeletedForAccountIds.Contains(accountIdStr)));

        if (conversation.ParticipantType == ChatParticipantType.SellerPartner)
        {
            legacyQuery = legacyQuery.Where(cm => cm.SenderRole != ChatSenderRole.SuperAdmin);
        }
        else if (conversation.ParticipantType == ChatParticipantType.Seller)
        {
            legacyQuery = legacyQuery.Where(cm => cm.SenderRole != ChatSenderRole.Partner);
        }

        var legacyList = await legacyQuery
            .Select(cm => new ChatMessageDto(
                cm.Id,
                cm.OrderRequestId,
                cm.SenderAccountId,
                cm.SenderRole.ToString(),
                cm.IsDeletedForEveryone ? "This message was deleted" : cm.Message,
                cm.IsRead,
                cm.ReadAt,
                cm.DeliveredAt,
                cm.SentAt,
                null,
                null,
                null,
                null,
                cm.IsEdited,
                cm.EditedAt,
                cm.IsDeletedForEveryone))
            .ToListAsync(cancellationToken);

        var seenIds = new HashSet<Guid>();
        var combined = new List<ChatMessageDto>();
        foreach (var m in modernList)
        {
            if (seenIds.Add(m.Id))
                combined.Add(m);
        }
        foreach (var m in legacyList)
        {
            if (seenIds.Add(m.Id))
                combined.Add(m);
        }

        var ordered = combined.OrderBy(m => m.SentAt).ToList();
        var totalCount = ordered.Count;

        // Chat pagination: Page 1 must return the latest messages in chronological order.
        // Higher page numbers paginate backwards to load older message history.
        int skip;
        int take;
        if (request.PageNumber <= 1)
        {
            skip = Math.Max(0, totalCount - request.PageSize);
            take = Math.Min(request.PageSize, totalCount);
        }
        else
        {
            var remainingBefore = totalCount - (request.PageNumber - 1) * request.PageSize;
            if (remainingBefore <= 0)
            {
                skip = 0;
                take = 0;
            }
            else
            {
                take = Math.Min(request.PageSize, remainingBefore);
                skip = Math.Max(0, remainingBefore - take);
            }
        }

        var paged = take > 0
            ? ordered.Skip(skip).Take(take).ToList()
            : new List<ChatMessageDto>();

        return Result.Success(new PagedList<ChatMessageDto>(
            paged, totalCount, request.PageNumber, request.PageSize));
    }
}
