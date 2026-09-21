namespace Eseller.Application.Features.Orders;

public record SellerOrderDto(
    Guid Id,
    Guid OrderRequestId,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    string Status,
    decimal TotalAmount,
    int TotalItems,
    List<OrderRequestItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt);