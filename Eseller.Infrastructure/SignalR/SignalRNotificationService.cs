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
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? notifyAccountIds = null)
    {
        // Always deliver into the open thread room.
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"order-{orderRequestId}"
        };

        var isAdminSender =
            senderRole.Equals(nameof(RoleType.SuperAdmin), StringComparison.OrdinalIgnoreCase) ||
            senderRole.Equals(nameof(RoleType.Partner), StringComparison.OrdinalIgnoreCase);

        // Sellers/customers messaging → ping all online admins (even if not in the room).
        // Admins already in the order room also receive this; clients dedupe by messageId.
        if (!isAdminSender)
        {
            groups.Add(nameof(RoleType.SuperAdmin));
            groups.Add(nameof(RoleType.Partner));
        }

        // Admin → specific seller/customer account inbox (account-{guid} joined on ChatHub connect).
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
                .ReceiveMessage(messageId, orderRequestId, senderAccountId, senderRole, message, sentAt);
        }
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
