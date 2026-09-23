namespace Eseller.Application.Features.AdminAuditLogs;

public record AuditLogDto(
    Guid Id,
    Guid? ActorAccountId,
    string? ActorEmail,
    string ActorType,
    string Action,
    string EntityType,
    Guid EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime CreatedAt);