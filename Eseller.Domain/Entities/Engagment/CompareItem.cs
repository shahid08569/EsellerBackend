using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Engagement;

public class CompareItem : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
}