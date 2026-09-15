using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Shipping;

public class Shipment : AuditableEntity
{
    public Guid SellerOrderId { get; set; }
    public Guid? ShippingMethodId { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? EstimatedDeliveryAt { get; set; }
}