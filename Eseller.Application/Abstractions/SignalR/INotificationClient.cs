namespace Eseller.Application.Abstractions.SignalR;

public interface INotificationClient
{
    Task ReceiveNotification(
        Guid notificationId,
        int type,
        string title,
        string message,
        DateTime createdAt);

    Task ReceiveOrderNotification(
        Guid orderRequestId,
        string customerName,
        int itemCount,
        decimal totalAmount,
        DateTime createdAt);
}