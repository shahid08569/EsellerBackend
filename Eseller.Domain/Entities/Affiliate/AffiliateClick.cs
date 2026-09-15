using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Affiliate;

public class AffiliateClick : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime ClickedAt { get; set; }
    public string? IpAddress { get; set; }
}