using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class Partner : AuditableEntity
{
    public Guid AccountId { get; set; }
    public Guid AddedBySuperAdminId { get; set; }
    public DateTime AddedAt { get; set; }
    public string? Permissions { get; set; } // JSON
}