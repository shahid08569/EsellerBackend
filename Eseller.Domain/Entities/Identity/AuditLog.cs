using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class AuditLog : BaseEntity
{
    public Guid? ActorAccountId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}