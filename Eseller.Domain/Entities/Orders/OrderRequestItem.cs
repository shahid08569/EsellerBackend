using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Orders;
public class OrderRequestItem : BaseEntity
{
    public Guid OrderRequestId { get; set; }
    public Guid SellerOrderId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }  
    public decimal PriceAtOrder { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string? VariantAttributesSnapshot { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal TotalPriceSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }  
}