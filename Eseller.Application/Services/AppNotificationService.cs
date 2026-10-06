using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Notifications;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Services;

public class AppNotificationService : IAppNotificationService
{
    public const string OutOfStockAlertType = "OutOfStock";

    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public AppNotificationService(
        IApplicationDbContext context,
        IDateTime dateTime,
        ISignalRNotificationService signalR)
    {
        _context = context;
        _dateTime = dateTime;
        _signalR = signalR;
    }

    public async Task NotifyAsync(
        Guid recipientUserId,
        RoleType recipientRole,
        NotificationType type,
        string title,
        string message,
        NotificationEntityType entityType,
        Guid? entityId,
        string? navigationUrl,
        string eventKey,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserId == Guid.Empty || string.IsNullOrWhiteSpace(eventKey))
            return;

        var key = eventKey.Length > 200 ? eventKey[..200] : eventKey;

        var exists = await _context.Notifications.AsNoTracking()
            .AnyAsync(n => n.EventKey == key, cancellationToken);
        if (exists) return;

        var now = _dateTime.UtcNow;
        var path = navigationUrl ?? DefaultNavigationUrl(type, entityId);
        var notif = new Notification
        {
            RecipientUserId = recipientUserId,
            RecipientRole = recipientRole,
            Type = type,
            Title = Truncate(title, 200),
            Message = Truncate(message, 2000),
            EntityType = entityType,
            EntityId = entityId,
            NavigationUrl = path is null ? null : Truncate(path, 500),
            IsRead = false,
            EventKey = key
        };
        notif.SetCreatedAt(now);
        notif.SetUpdatedAt(now);

        _context.Notifications.Add(notif);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent insert with same EventKey — idempotent no-op
            _context.Notifications.Remove(notif);
            return;
        }

        var unread = await _context.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == recipientUserId && !n.IsRead, cancellationToken);

        try
        {
            await _signalR.SendNotificationToAccountAsync(
                recipientUserId, notif.Id, notif.Type, notif.Title, notif.Message, cancellationToken);
            await _signalR.SendUnreadCountChangedAsync(recipientUserId, unread, cancellationToken);
        }
        catch
        {
            // Realtime is best-effort; DB row is source of truth
        }
    }

    public async Task NotifyAdminsAsync(
        NotificationType type,
        string title,
        string message,
        NotificationEntityType entityType,
        Guid? entityId,
        string? navigationUrl,
        string eventKeyPrefix,
        CancellationToken cancellationToken = default)
    {
        var admins = await _context.Accounts.AsNoTracking()
            .Where(a => a.RoleType == RoleType.SuperAdmin || a.RoleType == RoleType.Partner)
            .Select(a => new { a.Id, a.RoleType })
            .ToListAsync(cancellationToken);

        foreach (var admin in admins)
        {
            await NotifyAsync(
                admin.Id,
                admin.RoleType,
                type,
                title,
                message,
                entityType,
                entityId,
                navigationUrl,
                $"{eventKeyPrefix}:Admin:{admin.Id:N}",
                cancellationToken);
        }
    }

    public async Task NotifyProductOutOfStockOnceAsync(
        Guid productId,
        string productName,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty) return;

        var hasActive = await _context.ProductStockAlerts.AsNoTracking()
            .AnyAsync(a => a.ProductId == productId
                           && a.AlertType == OutOfStockAlertType
                           && a.ResolvedAt == null,
                cancellationToken);
        if (hasActive) return;

        var now = _dateTime.UtcNow;
        var alert = new ProductStockAlert
        {
            ProductId = productId,
            AlertType = OutOfStockAlertType,
            TriggeredAt = now
        };
        _context.ProductStockAlerts.Add(alert);
        await _context.SaveChangesAsync(cancellationToken);

        await NotifyAdminsAsync(
            NotificationType.ProductOutOfStock,
            "Product going out of stock",
            $"\"{productName}\" is now out of stock.",
            NotificationEntityType.Product,
            productId,
            $"/products?status=all&highlight={productId}",
            $"ProductOutOfStock:{alert.Id:N}",
            cancellationToken);
    }

    public async Task ResolveProductStockAlertAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty) return;

        var active = await _context.ProductStockAlerts
            .Where(a => a.ProductId == productId
                        && a.AlertType == OutOfStockAlertType
                        && a.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        if (active.Count == 0) return;

        var now = _dateTime.UtcNow;
        foreach (var alert in active)
            alert.ResolvedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string DefaultNavigationUrl(NotificationType type, Guid? entityId) => type switch
    {
        // List routes only — admin/seller apps do not have :id detail children yet
        NotificationType.NewSeller => "/sellers",
        NotificationType.NewCustomer => "/customers",
        NotificationType.NewOrder => "/orders",
        NotificationType.NewMessage => entityId.HasValue ? $"/chat?c={entityId}" : "/chat",
        NotificationType.ProductListingRequest => "/products?status=pending",
        NotificationType.ProductOutOfStock => entityId.HasValue
            ? $"/products?status=all&highlight={entityId}"
            : "/products",
        NotificationType.TierUpdated => "/shops?view=tiers",
        NotificationType.OrderDelivered => "/orders",
        NotificationType.PaymentCompleted => "/earnings",
        NotificationType.CommissionEarned => "/earnings",
        NotificationType.TierBadgeRequestUpdated => "/dashboard",
        NotificationType.ProductListingRequestUpdated => "/products",
        NotificationType.NewProduct => "/products",
        _ => "/dashboard"
    };

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}
