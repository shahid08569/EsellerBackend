using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface IAppNotificationService
{
    /// <summary>
    /// Creates a notification if <paramref name="eventKey"/> is new (idempotent).
    /// Persists to DB then pushes via SignalR.
    /// </summary>
    Task NotifyAsync(
        Guid recipientUserId,
        RoleType recipientRole,
        NotificationType type,
        string title,
        string message,
        NotificationEntityType entityType,
        Guid? entityId,
        string? navigationUrl,
        string eventKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all SuperAdmin (+ Partner) accounts. EventKey is suffixed per recipient.
    /// </summary>
    Task NotifyAdminsAsync(
        NotificationType type,
        string title,
        string message,
        NotificationEntityType entityType,
        Guid? entityId,
        string? navigationUrl,
        string eventKeyPrefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Out-of-stock: create ProductStockAlert once per stock-out cycle, then notify admins once.
    /// </summary>
    Task NotifyProductOutOfStockOnceAsync(
        Guid productId,
        string productName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Call when stock returns above zero — resolves active ProductStockAlert so a future stock-out can notify again.
    /// </summary>
    Task ResolveProductStockAlertAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
