using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Orders;

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }  
    public DateTime CreatedAt { get; set; }
}