namespace Eseller.Application.Features.Dashboard;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    string? NavigationUrl = null,
    Guid? EntityId = null,
    string? EntityType = null,
    // Backward-compatible aliases for existing Angular clients
    string? LinkPath = null,
    Guid? RelatedEntityId = null);
