namespace Eseller.Application.Features.ShopCategories;

public record ShopCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? BadgeText,
    string? BadgeColor,
    string? IconUrl,
    int DisplayOrder,
    bool IsActive,
    int ShopsCount,
    DateTime CreatedAt,
    decimal PriceUsd = 0,
    int MaxProductListings = 200,
    bool IsRecommended = false);
