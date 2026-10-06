using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Notifications;

/// <summary>
/// Universal notification row — Super Admin and Seller share one table.
/// </summary>
public class Notification : AuditableEntity
{
    /// <summary>Recipient Account.Id (JWT subject).</summary>
    public Guid RecipientUserId { get; set; }

    public RoleType RecipientRole { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }
    public NotificationEntityType EntityType { get; set; } = NotificationEntityType.None;

    public string? NavigationUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>Idempotency key, e.g. OrderDelivered:{orderId}:{sellerAccountId}.</summary>
    public string EventKey { get; set; } = string.Empty;
}
