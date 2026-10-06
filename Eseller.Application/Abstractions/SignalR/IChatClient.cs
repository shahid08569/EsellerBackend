namespace Eseller.Application.Abstractions.SignalR;

public interface IChatClient
{
    /// <summary>
    /// Real-time message. conversationId is also exposed as orderRequestId on older clients
    /// (same Guid for migrated seller/customer rooms).
    /// </summary>
    Task ReceiveMessage(
        Guid messageId,
        Guid orderRequestId,
        Guid senderAccountId,
        string senderRole,
        string message,
        DateTime sentAt,
        string? attachmentUrl,
        string? attachmentFileName,
        string? attachmentContentType);

    Task MessageRead(Guid messageId, DateTime readAt);

    Task UnreadCountChanged(Guid conversationId, int unreadCount);
}
