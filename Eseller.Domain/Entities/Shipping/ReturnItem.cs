using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Shipping;

public class ReturnItem : BaseEntity
{
    public Guid ReturnRequestId { get; set; }
    public Guid OrderRequestItemId { get; set; }
    public int Quantity { get; set; }
}