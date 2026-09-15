namespace Eseller.Domain.Enums;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Processing = 3,
    Packed = 4,
    Shipped = 5,
    OutForDelivery = 6,
    Delivered = 7,
    Cancelled = 8,
    ReturnRequested = 9,
    Returned = 10,
    RefundPending = 11,
    Refunded = 12
}