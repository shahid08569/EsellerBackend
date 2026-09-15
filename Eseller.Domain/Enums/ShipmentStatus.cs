namespace Eseller.Domain.Enums;

public enum ShipmentStatus
{
    Pending = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5,
    Returned = 6
}