using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Promotions;

public class Coupon : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscount { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public CouponScopeType ScopeType { get; set; } = CouponScopeType.Platform;
    public Guid? CreatedByAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}