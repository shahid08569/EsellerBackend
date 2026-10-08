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
    DateTime CreatedAt,
    ProductBadgeDto? Badges,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? SourceProductId = null,
    Guid? ShopId = null,
    /// <summary>SuperAdmin platform rating for the store (0–5).</summary>
    decimal ShopRating = 0,
    /// <summary>Seller tier badge text (Gold / Diamond / Bronze…).</summary>
    string? ShopBadgeText = null,
    /// <summary>Seller tier badge color hex.</summary>
    string? ShopBadgeColor = null,
    /// <summary>Short product description for cards (optional).</summary>
    string? Description = null,
    /// <summary>Master Warehouse: opted-in to customer website (sellers ignore).</summary>
    bool IsStorefrontLive = false,
    /// <summary>Approved reviewer count for storefront cards.</summary>
    int ReviewCount = 0);
