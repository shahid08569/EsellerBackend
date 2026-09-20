using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class Affiliate : AuditableEntity
{
    public Guid AccountId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;

    public decimal? CommissionRate { get; set; }
}