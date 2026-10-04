namespace Eseller.Application.Features.Orders;

public record OrderRequestDto(
    Guid Id,
    string Status,
    decimal TotalAmount,
    int TotalItems,
    Guid? AffiliateId,
    string? ReferralCodeSnapshot,
    List<OrderRequestItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ShippingAddress = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? OrderNotes = null);