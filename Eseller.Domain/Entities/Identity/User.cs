using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class User : AuditableEntity
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
}