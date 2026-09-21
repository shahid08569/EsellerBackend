using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Affiliate;

public class AffiliateCommission : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid OrderRequestId { get; set; }
    public Guid OrderRequestItemId { get; set; }
    public Guid? AffiliateClickId { get; set; }
    public decimal CommissionAmount { get; set; }
    public AffiliateCommissionStatus Status { get; set; } = AffiliateCommissionStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}