using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface ISignalRNotificationService
{
    Task BroadcastChatMessageAsync(
        Guid orderRequestId,
        Guid messageId,
        Guid senderAccountId,
        string senderRole,
        string message,
        DateTime sentAt,
        CancellationToken cancellationToken);

    Task SendNotificationToAccountAsync(
        Guid accountId,
        Guid notificationId,
        NotificationType type,
        string title,
        string message,
        CancellationToken cancellationToken);

    Task BroadcastNewOrderAsync(
        Guid orderRequestId,
        string customerName,
        int itemCount,
        decimal totalAmount,
        CancellationToken cancellationToken);
}