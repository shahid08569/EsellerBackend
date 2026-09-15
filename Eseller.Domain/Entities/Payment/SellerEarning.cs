using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Payment;

public class SellerEarning : BaseEntity
{
    public Guid SellerOrderId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public Guid? CommissionRuleId { get; set; }
    public decimal NetAmount { get; set; }
    public SellerEarningStatus Status { get; set; } = SellerEarningStatus.Pending;
}