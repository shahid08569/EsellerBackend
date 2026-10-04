namespace Eseller.Application.Features.Products;

public record ProductDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    Guid CategoryId,
    string CategoryName,
    Guid? BrandId,
    string? BrandName,
    string Name,
    string Slug,
    string? Description,
    decimal BasePrice,
    bool IsFeatured,
    bool IsApproved,
    string Status,
    string? RejectionReason,
    int ViewCount,
    decimal? AvgRating,
    string? SeoTitle,
    string? SeoDescription,
    bool IsActive,
    DateTime CreatedAt,
    /// <summary>SuperAdmin platform rating for the store (0–5).</summary>
    decimal ShopRating = 0);