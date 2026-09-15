using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Promotions;

public class CouponCustomer : BaseEntity
{
    public Guid CouponId { get; set; }
    public Guid UserId { get; set; }
}