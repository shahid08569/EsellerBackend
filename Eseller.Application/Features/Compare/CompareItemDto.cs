namespace Eseller.Application.Features.Compare;

public record CompareItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string? ProductImage,
    decimal BasePrice,
    decimal? DiscountedPrice,
    string? BrandName,
    decimal? AvgRating,
    string ShopName,
    string ShopSlug,
    Guid ShopId,
    bool IsApproved,
    bool IsActive,
    int TotalStock,
    DateTime CreatedAt);