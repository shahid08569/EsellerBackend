using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Promotions;

public class FlashSaleProduct : BaseEntity
{
    public Guid FlashSaleId { get; set; }
    public Guid ProductId { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
}