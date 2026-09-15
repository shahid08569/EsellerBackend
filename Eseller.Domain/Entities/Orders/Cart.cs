using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Orders;

public class Cart : AuditableEntity
{
    public Guid UserId { get; set; }
}