namespace Eseller.Application.Features.Orders;

public record OrderTrackingDto(
    Guid OrderRequestId,
    string CurrentStatus,
    DateTime CreatedAt,
    List<OrderStatusHistoryDto> Timeline);