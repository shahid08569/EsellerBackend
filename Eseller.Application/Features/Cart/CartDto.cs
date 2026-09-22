namespace Eseller.Application.Features.Cart;

public record CartDto(
    Guid Id,
    List<CartItemDto> Items,
    int TotalItems,
    decimal SubTotal);