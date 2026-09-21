using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class Affiliate : AuditableEntity
{
    public Guid AccountId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public decimal? CommissionRate { get; set; }
    public int TotalClicks { get; set; } = 0;
    public int TotalConversions { get; set; } = 0;
    public decimal TotalEarned { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}