using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Catalog;

public class Discount : AuditableEntity
{
    public Guid? ProductId { get; set; }
    public DiscountScopeType ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}