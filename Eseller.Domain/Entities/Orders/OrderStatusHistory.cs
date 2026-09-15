using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Orders;

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderRequestId { get; set; }
    public OrderStatus OldStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public Guid? ChangedByAccountId { get; set; }
    public string ChangedByType { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}