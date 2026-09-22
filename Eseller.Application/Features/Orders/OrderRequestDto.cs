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
    DateTime UpdatedAt);