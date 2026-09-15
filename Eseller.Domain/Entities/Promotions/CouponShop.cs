using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Promotions;

public class CouponShop : BaseEntity
{
    public Guid CouponId { get; set; }
    public Guid ShopId { get; set; }
}