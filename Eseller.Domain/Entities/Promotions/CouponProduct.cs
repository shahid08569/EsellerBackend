using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Promotions;

public class CouponProduct : BaseEntity
{
    public Guid CouponId { get; set; }
    public Guid ProductId { get; set; }
}