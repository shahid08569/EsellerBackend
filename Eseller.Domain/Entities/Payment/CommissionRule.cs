using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Payment;

public class CommissionRule : AuditableEntity
{
    public CommissionScopeType ScopeType { get; set; }
    
    public Guid? ScopeId { get; set; }
    public CommissionType Type { get; set; }
    public decimal Value { get; set; }
    public bool IsActive { get; set; } = true;
}