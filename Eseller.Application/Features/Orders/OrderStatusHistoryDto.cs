namespace Eseller.Application.Features.Orders;

public record OrderStatusHistoryDto(
    Guid Id,
    Guid OrderRequestId,
    string OldStatus,
    string NewStatus,
    Guid? ChangedByAccountId,
    string ChangedByType,
    DateTime ChangedAt);