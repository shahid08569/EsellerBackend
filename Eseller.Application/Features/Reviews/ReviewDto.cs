namespace Eseller.Application.Features.Reviews;

public record ReviewDto(
    Guid Id,
    Guid ProductId,
    Guid UserId,
    string UserName,
    Guid? OrderRequestItemId,
    bool IsVerifiedPurchase,
    int Rating,
    string? Title,
    string? Comment,
    bool IsApproved,
    DateTime CreatedAt,
    string? ProductName = null,
    string? ShopName = null);
