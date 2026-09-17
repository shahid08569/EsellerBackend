namespace Eseller.Domain.Common;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void SetCreatedAt(DateTime timestamp) => CreatedAt = timestamp;
    public void SetUpdatedAt(DateTime timestamp) => UpdatedAt = timestamp;
}