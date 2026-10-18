using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Chat;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.SendChatMessage;

public class SendChatMessageCommandHandler
    : IRequestHandler<SendChatMessageCommand, Result<SendChatMessageResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _notificationService;
    private readonly IAppNotificationService _appNotifications;
    private readonly Eseller.Application.Abstractions.SignalR.IUserPresenceTracker _presenceTracker;

    public SendChatMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService notificationService,
        IAppNotificationService appNotifications,
        Eseller.Application.Abstractions.SignalR.IUserPresenceTracker presenceTracker)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notificationService = notificationService;
        _appNotifications = appNotifications;
        _presenceTracker = presenceTracker;
    }

    public async Task<Result<SendChatMessageResult>> Handle(
        SendChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<SendChatMessageResult>("User is not authenticated.", "UNAUTHENTICATED");

        var conversation = await ResolveConversationAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
            return Result.Failure<SendChatMessageResult>("Conversation not found.", "CONVERSATION_NOT_FOUND");

        if (!await SupportConversationService.CanAccessAsync(
                _context, _currentUserService, conversation, cancellationToken))
            return Result.Failure<SendChatMessageResult>("You do not have permission.", "FORBIDDEN");

        // Role matrix:
        // Customer ↔ SuperAdmin (Customer)
        // Seller ↔ SuperAdmin (Seller)
        // Seller ↔ Referring Partner (SellerPartner)
        // Partner ↔ SuperAdmin (Partner)
        var role = _currentUserService.RoleType;
        if (role is null || role is not (RoleType.SuperAdmin or RoleType.Partner or RoleType.Shopkeeper or RoleType.User))
            return Result.Failure<SendChatMessageResult>("You do not have permission.", "FORBIDDEN");

        if (role == RoleType.Shopkeeper &&
            conversation.ParticipantType != ChatParticipantType.Seller &&
            conversation.ParticipantType != ChatParticipantType.SellerPartner)
            return Result.Failure<SendChatMessageResult>("Sellers can only message Super Admin or their Partner.", "FORBIDDEN");

        if (role == RoleType.Partner &&
            conversation.ParticipantType != ChatParticipantType.Partner &&
            conversation.ParticipantType != ChatParticipantType.SellerPartner)
            return Result.Failure<SendChatMessageResult>("Partners can only message Super Admin or their referred shops.", "FORBIDDEN");

        if (role == RoleType.User && conversation.ParticipantType != ChatParticipantType.Customer)
            return Result.Failure<SendChatMessageResult>("Customers can only message Super Admin.", "FORBIDDEN");

        if (role == RoleType.SuperAdmin && conversation.ParticipantType == ChatParticipantType.SellerPartner)
            return Result.Failure<SendChatMessageResult>("This conversation between the partner and referred merchant is read-only for Super Admin.", "READ_ONLY_CONVERSATION");

        var senderRole = SupportConversationService.MapSenderRole(role.Value);
        var now = _dateTime.UtcNow;
        var text = (request.Message ?? string.Empty).Trim();

        var notifyAccountIds = await ResolveNotifyAccountIdsAsync(
            conversation, _currentUserService.AccountId.Value, senderRole, cancellationToken);

        // Check if recipient is online in SignalR
        var isRecipientOnline = _presenceTracker.IsAnyUserOnline(notifyAccountIds);
        var deliveredAt = isRecipientOnline ? now : (DateTime?)null;

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = _currentUserService.AccountId.Value,
            SenderRole = senderRole,
            Content = text,
            SentAt = now,
            IsRead = false,
            DeliveredAt = deliveredAt,
            AttachmentUrl = string.IsNullOrWhiteSpace(request.AttachmentUrl) ? null : request.AttachmentUrl.Trim(),
            AttachmentFileName = string.IsNullOrWhiteSpace(request.AttachmentFileName) ? null : request.AttachmentFileName.Trim(),
            AttachmentContentType = string.IsNullOrWhiteSpace(request.AttachmentContentType) ? null : request.AttachmentContentType.Trim(),
            AttachmentSizeBytes = request.AttachmentSizeBytes
        };

        _context.Messages.Add(message);
        conversation.LastMessageAt = now;
        conversation.SetUpdatedAt(now);
        await _context.SaveChangesAsync(cancellationToken);

        var cleanPreviewText = System.Text.RegularExpressions.Regex.Replace(text, @"^\[reply:[^:]+:[^\]]+\][\s\S]*?\[/reply\]\s*", string.Empty).Trim();
        var preview = string.IsNullOrWhiteSpace(cleanPreviewText)
            ? (message.AttachmentFileName is not null ? $"📎 {message.AttachmentFileName}" : "Attachment")
            : (cleanPreviewText.Length > 120 ? cleanPreviewText[..117] + "..." : cleanPreviewText);

        var senderLabel = SupportConversationService.IsAdminRole(senderRole) ? "Admin"
            : senderRole == ChatSenderRole.Shopkeeper ? "Seller" : "Customer";

        // Important: sellers never get customer↔admin message notifications (resolved above)
        foreach (var accountId in notifyAccountIds)
        {
            var recipientRole = await _context.Accounts.AsNoTracking()
                .Where(a => a.Id == accountId)
                .Select(a => a.RoleType)
                .FirstOrDefaultAsync(cancellationToken);

            // Customer recipients are out of scope for this notification system
            if (recipientRole == RoleType.User) continue;

            await _appNotifications.NotifyAsync(
                accountId,
                recipientRole,
                NotificationType.NewMessage,
                $"New message from {senderLabel}",
                preview,
                NotificationEntityType.Conversation,
                conversation.Id,
                $"/chat?c={conversation.Id}",
                $"NewMessage:{message.Id:N}:Recipient:{accountId:N}",
                cancellationToken);
        }

        // DB saved first — then real-time broadcast
        await _notificationService.BroadcastChatMessageAsync(
            conversation.Id,
            message.Id,
            message.SenderId,
            senderRole.ToString(),
            string.IsNullOrWhiteSpace(text) ? preview : text,
            now,
            cancellationToken,
            notifyAccountIds,
            message.AttachmentUrl,
            message.AttachmentFileName,
            message.AttachmentContentType);

        if (conversation.ParticipantId != Guid.Empty
            && conversation.ParticipantId != conversation.Id
            && conversation.ParticipantType != ChatParticipantType.SellerPartner
            && conversation.ParticipantType != ChatParticipantType.Partner)
        {
            await _notificationService.BroadcastChatMessageAsync(
                conversation.ParticipantId,
                message.Id,
                message.SenderId,
                senderRole.ToString(),
                string.IsNullOrWhiteSpace(text) ? preview : text,
                now,
                cancellationToken,
                notifyAccountIds,
                message.AttachmentUrl,
                message.AttachmentFileName,
                message.AttachmentContentType);
        }

        return Result.Success(new SendChatMessageResult(message.Id, isRecipientOnline, deliveredAt));
    }

    private async Task<Conversation?> ResolveConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (existing is not null)
            return existing;

        var role = _currentUserService.RoleType;

        // Lazy ensure: shop id
        var shopExists = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(s => s.Id == conversationId && s.DeletedAt == null, cancellationToken);
        if (shopExists)
        {
            var pType = role == RoleType.Partner ? ChatParticipantType.SellerPartner : ChatParticipantType.Seller;
            return await SupportConversationService.GetOrCreateAsync(
                _context, conversationId, pType, _dateTime.UtcNow, cancellationToken);
        }

        var partner = await _context.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == conversationId || (_currentUserService.AccountId.HasValue && p.AccountId == _currentUserService.AccountId.Value), cancellationToken);
        if (partner != null && (conversationId == partner.Id || role == RoleType.Partner))
        {
            return await SupportConversationService.GetOrCreateAsync(
                _context, partner.Id, ChatParticipantType.Partner, _dateTime.UtcNow, cancellationToken);
        }

        var userExists = await _context.Users.AsNoTracking()
            .AnyAsync(u => u.Id == conversationId, cancellationToken);
        if (userExists)
        {
            return await SupportConversationService.GetOrCreateAsync(
                _context, conversationId, ChatParticipantType.Customer, _dateTime.UtcNow, cancellationToken);
        }

        // Legacy customer support OrderRequest id → map to User participant conversation
        var legacy = await _context.OrderRequests.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == conversationId
                && o.ShippingAddress != null
                && (o.ShippingAddress.StartsWith("CUSTOMER_SUPPORT_")
                    || o.ShippingAddress == "Live Support Inquiry"),
                cancellationToken);

        if (legacy is not null && legacy.UserId != Guid.Empty)
        {
            return await SupportConversationService.GetOrCreateAsync(
                _context, legacy.UserId, ChatParticipantType.Customer, _dateTime.UtcNow, cancellationToken);
        }

        return null;
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveNotifyAccountIdsAsync(
        Conversation conversation,
        Guid senderAccountId,
        ChatSenderRole senderRole,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();

        // 1. SellerPartner: Shopkeeper ↔ Referring Partner (strictly isolated from SuperAdmin & other partners)
        if (conversation.ParticipantType == ChatParticipantType.SellerPartner)
        {
            if (senderRole == ChatSenderRole.Shopkeeper)
            {
                // Notify ONLY the shop's referring partner
                var partnerAccountId = await (
                    from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
                    join p in _context.Partners.AsNoTracking() on s.PartnerId equals p.Id
                    where s.Id == conversation.ParticipantId && s.DeletedAt == null
                    select p.AccountId
                ).FirstOrDefaultAsync(cancellationToken);

                if (partnerAccountId != Guid.Empty && partnerAccountId != senderAccountId)
                    ids.Add(partnerAccountId);
            }
            else
            {
                // Partner sending to referred seller: notify ONLY this shop's shopkeeper
                var shopkeeperAccountId = await (
                    from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
                    join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
                    where s.Id == conversation.ParticipantId && s.DeletedAt == null
                    select sk.AccountId
                ).FirstOrDefaultAsync(cancellationToken);

                if (shopkeeperAccountId != Guid.Empty && shopkeeperAccountId != senderAccountId)
                    ids.Add(shopkeeperAccountId);
            }

            return ids.ToList();
        }

        // 2. Partner: Partner ↔ SuperAdmin
        if (conversation.ParticipantType == ChatParticipantType.Partner)
        {
            if (senderRole == ChatSenderRole.Partner)
            {
                // Notify SuperAdmins only
                var superAdminIds = await _context.Accounts.AsNoTracking()
                    .Where(a => a.RoleType == RoleType.SuperAdmin)
                    .Select(a => a.Id)
                    .ToListAsync(cancellationToken);
                foreach (var id in superAdminIds)
                {
                    if (id != senderAccountId)
                        ids.Add(id);
                }
            }
            else
            {
                // SuperAdmin sending to Partner: notify ONLY this partner
                var partnerAccountId = await _context.Partners.AsNoTracking()
                    .Where(p => p.Id == conversation.ParticipantId)
                    .Select(p => p.AccountId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (partnerAccountId != Guid.Empty && partnerAccountId != senderAccountId)
                    ids.Add(partnerAccountId);
            }

            return ids.ToList();
        }

        // 3. Customer: Customer ↔ SuperAdmin
        if (conversation.ParticipantType == ChatParticipantType.Customer)
        {
            if (senderRole == ChatSenderRole.User)
            {
                // Notify SuperAdmins only
                var superAdminIds = await _context.Accounts.AsNoTracking()
                    .Where(a => a.RoleType == RoleType.SuperAdmin)
                    .Select(a => a.Id)
                    .ToListAsync(cancellationToken);
                foreach (var id in superAdminIds)
                {
                    if (id != senderAccountId)
                        ids.Add(id);
                }
            }
            else
            {
                // Admin sending to Customer: notify user account
                var accountId = await _context.Users.AsNoTracking()
                    .Where(u => u.Id == conversation.ParticipantId)
                    .Select(u => u.AccountId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (accountId != Guid.Empty && accountId != senderAccountId)
                    ids.Add(accountId);
            }

            return ids.ToList();
        }

        // 4. Seller: Seller ↔ SuperAdmin (Customer Support)
        if (conversation.ParticipantType == ChatParticipantType.Seller)
        {
            if (senderRole == ChatSenderRole.Shopkeeper)
            {
                // Notify SuperAdmins ONLY (Partners are NEVER notified for platform support!)
                var superAdminIds = await _context.Accounts.AsNoTracking()
                    .Where(a => a.RoleType == RoleType.SuperAdmin)
                    .Select(a => a.Id)
                    .ToListAsync(cancellationToken);
                foreach (var id in superAdminIds)
                {
                    if (id != senderAccountId)
                        ids.Add(id);
                }
            }
            else
            {
                // SuperAdmin sending to Seller: notify shopkeeper
                var accountId = await (
                    from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
                    join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
                    where s.Id == conversation.ParticipantId && s.DeletedAt == null
                    select sk.AccountId
                ).FirstOrDefaultAsync(cancellationToken);
                if (accountId != Guid.Empty && accountId != senderAccountId)
                    ids.Add(accountId);
            }

            return ids.ToList();
        }

        return ids.ToList();
    }
}
