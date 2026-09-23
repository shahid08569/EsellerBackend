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
        Guid orderRequestId,
        Guid messageId,
        Guid senderAccountId,
        string senderRole,
        string message,
        DateTime sentAt,
        CancellationToken cancellationToken)
    {
        await _chatHubContext.Clients
            .Group($"order-{orderRequestId}")
            .ReceiveMessage(messageId, orderRequestId, senderAccountId, senderRole, message, sentAt);
    }

    public async Task SendNotificationToAccountAsync(
        Guid accountId,
        Guid notificationId,
        NotificationType type,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        await _notificationHubContext.Clients
            .Group($"account-{accountId}")
            .ReceiveNotification(notificationId, (int)type, title, message, DateTime.UtcNow);
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
}