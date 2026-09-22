namespace Eseller.Application.Features.Search;

public record SearchResultDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    decimal BasePrice,
    decimal? MinVariantPrice,
    decimal? MaxVariantPrice,
    decimal? AvgRating,
    int ViewCount,
    bool IsFeatured,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    Guid CategoryId,
    string CategoryName,
    Guid? BrandId,
    string? BrandName,
    string? PrimaryImageUrl,
    int TotalVariants,
    int AvailableStock);