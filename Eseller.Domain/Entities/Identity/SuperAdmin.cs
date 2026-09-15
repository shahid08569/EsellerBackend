using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class SuperAdmin : AuditableEntity
{
    public Guid AccountId { get; set; }
}