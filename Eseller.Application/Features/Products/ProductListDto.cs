namespace Eseller.Application.Features.Products;

public record ProductListDto(
    Guid Id,
    string Name,
    string Slug,
    decimal BasePrice,
    string? PrimaryImageUrl,
    string ShopName,
    string ShopSlug,
    string CategoryName,
    string? BrandName,
    decimal? AvgRating,
    bool IsFeatured,
    bool IsApproved,
    string Status,
    string? RejectionReason,
    DateTime CreatedAt);