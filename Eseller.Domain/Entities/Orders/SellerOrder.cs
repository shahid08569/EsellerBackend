using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Orders;

public class SellerOrder : AuditableEntity
{
    public Guid OrderRequestId { get; set; }
    public Guid ShopId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
}