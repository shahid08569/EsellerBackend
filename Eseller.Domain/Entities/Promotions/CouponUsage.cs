using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Promotions;

public class CouponUsage : BaseEntity
{
    public Guid CouponId { get; set; }
    public Guid OrderRequestId { get; set; }
    public Guid UserId { get; set; }
    public DateTime UsedAt { get; set; }
}