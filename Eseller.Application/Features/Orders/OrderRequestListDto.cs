namespace Eseller.Application.Features.Orders;

public record OrderRequestListDto(
    Guid Id,
    string Status,
    decimal TotalAmount,
    int TotalItems,
    int TotalShops,
    DateTime CreatedAt);