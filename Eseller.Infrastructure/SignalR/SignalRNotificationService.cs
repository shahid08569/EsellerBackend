using Eseller.Application.Abstractions;
using Eseller.Application.Abstractions.SignalR;
using Eseller.Domain.Enums;
using Eseller.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Eseller.Infrastructure.SignalR;

public class SignalRNotificationService : ISignalRNotificationService
{
    private readonly IHubContext<ChatHub, IChatClient> _chatHubContext;
    private readonly IHubContext<NotificationHub, INotificationClient> _notificationHubContext;

    public SignalRNotificationService(
        IHubContext<ChatHub, IChatClient> chatHubContext,
        IHubContext<NotificationHub, INotificationClient> notificationHubContext)
    {
        _chatHubContext = chatHubContext;
        _notificationHubContext = notificationHubContext;
    }

    public async Task BroadcastChatMessageAsync(
        Guid conversationId,
        Guid messageId,
        Guid senderAccountId,
        string senderRole,
        string message,
        DateTime sentAt,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null,
        string? attachmentUrl = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"conversation:{conversationId}",
            $"order-{conversationId}" // legacy Angular JoinOrderRoom
        };

        var isAdminSender =
            senderRole.Equals(nameof(RoleType.SuperAdmin), StringComparison.OrdinalIgnoreCase) ||
            senderRole.Equals(nameof(RoleType.Partner), StringComparison.OrdinalIgnoreCase);

        // Never broadcast to all partners globally! Scoped recipients are handled via notifyAccountIds.
        if (!isAdminSender && notifyAccountIds != null && notifyAccountIds.Count > 0)
        {
            // Only add SuperAdmin group if not a partner-specific chat
        }

        if (notifyAccountIds is { Count: > 0 })
        {
            foreach (var accountId in notifyAccountIds.Distinct())
            {
                if (accountId != Guid.Empty && accountId != senderAccountId)
                    groups.Add($"account-{accountId}");
            }
        }

        foreach (var group in groups)
        {
            await _chatHubContext.Clients
                .Group(group)
                .ReceiveMessage(
                    messageId,
                    conversationId,
                    senderAccountId,
                    senderRole,
                    message,
                    sentAt,
                    attachmentUrl,
                    attachmentFileName,
                    attachmentContentType);
        }
    }

    public async Task BroadcastUnreadCountChangedAsync(
        Guid conversationId,
        int conversationUnread,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        foreach (var accountId in accountIds.Distinct())
        {
            if (accountId == Guid.Empty) continue;
            await _chatHubContext.Clients
                .Group($"account-{accountId}")
                .UnreadCountChanged(conversationId, conversationUnread);
        }

        await _chatHubContext.Clients
            .Group($"conversation:{conversationId}")
            .UnreadCountChanged(conversationId, conversationUnread);
    }

    public async Task SendNotificationToAccountAsync(
        Guid accountId,
        Guid notificationId,
        NotificationType type,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        // Architecture: user:{UserId}; keep account-{id} for legacy clients
        var clients = _notificationHubContext.Clients;
        await clients.Group($"user:{accountId}")
            .ReceiveNotification(notificationId, (int)type, title, message, DateTime.UtcNow);
        await clients.Group($"account-{accountId}")
            .ReceiveNotification(notificationId, (int)type, title, message, DateTime.UtcNow);
    }

    public async Task SendUnreadCountChangedAsync(
        Guid accountId,
        int unreadCount,
        CancellationToken cancellationToken)
    {
        await _notificationHubContext.Clients.Group($"user:{accountId}")
            .UnreadCountChanged(unreadCount);
        await _notificationHubContext.Clients.Group($"account-{accountId}")
            .UnreadCountChanged(unreadCount);
    }

    public async Task BroadcastNewOrderAsync(
        Guid orderRequestId,
        string customerName,
        int itemCount,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        await _notificationHubContext.Clients
            .Group("SuperAdmin")
            .ReceiveOrderNotification(orderRequestId, customerName, itemCount, totalAmount, DateTime.UtcNow);
    }

    public async Task BroadcastMessageDeliveredAsync(
        Guid conversationId,
        Guid messageId,
        DateTime deliveredAt,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"conversation:{conversationId}",
            $"order-{conversationId}"
        };

        if (notifyAccountIds is { Count: > 0 })
        {
            foreach (var accountId in notifyAccountIds.Distinct())
            {
                if (accountId != Guid.Empty)
                    groups.Add($"account-{accountId}");
            }
        }

        foreach (var group in groups)
        {
            await _chatHubContext.Clients
                .Group(group)
                .MessageDelivered(messageId, deliveredAt);
        }
    }

    public async Task BroadcastMessageReadAsync(
        Guid conversationId,
        Guid messageId,
        DateTime readAt,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"conversation:{conversationId}",
            $"order-{conversationId}"
        };

        if (notifyAccountIds is { Count: > 0 })
        {
            foreach (var accountId in notifyAccountIds.Distinct())
            {
                if (accountId != Guid.Empty)
                    groups.Add($"account-{accountId}");
            }
        }

        foreach (var group in groups)
        {
            await _chatHubContext.Clients
                .Group(group)
                .MessageRead(messageId, readAt);
        }
    }

    public async Task BroadcastMessageEditedAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        DateTime editedAt,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"conversation:{conversationId}",
            $"order-{conversationId}"
        };

        if (notifyAccountIds is { Count: > 0 })
        {
            foreach (var accountId in notifyAccountIds.Distinct())
            {
                if (accountId != Guid.Empty)
                    groups.Add($"account-{accountId}");
            }
        }

        foreach (var group in groups)
        {
            await _chatHubContext.Clients
                .Group(group)
                .MessageEdited(messageId, conversationId, newContent, editedAt);
        }
    }

    public async Task BroadcastMessageDeletedAsync(
        Guid conversationId,
        Guid messageId,
        bool isDeletedForEveryone,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"conversation:{conversationId}",
            $"order-{conversationId}"
        };

        if (notifyAccountIds is { Count: > 0 })
        {
            foreach (var accountId in notifyAccountIds.Distinct())
            {
                if (accountId != Guid.Empty)
                    groups.Add($"account-{accountId}");
            }
        }

        foreach (var group in groups)
        {
            await _chatHubContext.Clients
                .Group(group)
                .MessageDeleted(messageId, conversationId, isDeletedForEveryone);
        }
    }
}
