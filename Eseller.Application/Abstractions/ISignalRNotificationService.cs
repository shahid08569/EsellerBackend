using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface ISignalRNotificationService
{
    Task BroadcastChatMessageAsync(
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
        string? attachmentContentType = null);

    Task SendNotificationToAccountAsync(
        Guid accountId,
        Guid notificationId,
        NotificationType type,
        string title,
        string message,
        CancellationToken cancellationToken);

    Task SendUnreadCountChangedAsync(
        Guid accountId,
        int unreadCount,
        CancellationToken cancellationToken);

    Task BroadcastNewOrderAsync(
        Guid orderRequestId,
        string customerName,
        int itemCount,
        decimal totalAmount,
        CancellationToken cancellationToken);

    Task BroadcastUnreadCountChangedAsync(
        Guid conversationId,
        int conversationUnread,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);

    /// <summary>Notify message sender(s) that their outbound message was read (WhatsApp-style ✓✓).</summary>
    Task BroadcastMessageReadAsync(
        Guid conversationId,
        Guid messageId,
        DateTime readAt,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null);
}
