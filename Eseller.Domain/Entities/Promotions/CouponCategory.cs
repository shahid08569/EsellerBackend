using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Promotions;

public class CouponCategory : BaseEntity
{
    public Guid CouponId { get; set; }
    public Guid CategoryId { get; set; }
}