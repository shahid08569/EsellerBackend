namespace Eseller.Application.Features.Wishlist;

public record WishlistItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string? ProductImage,
    decimal BasePrice,
    decimal? DiscountedPrice,
    decimal? AvgRating,
    string ShopName,
    string ShopSlug,
    Guid ShopId,
    bool IsApproved,
    bool IsActive,
    DateTime CreatedAt);