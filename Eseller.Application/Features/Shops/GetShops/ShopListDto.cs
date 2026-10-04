namespace Eseller.Application.Features.Shops;

public record ShopListDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? City,
    string? Country,
    decimal Rating,
    int TotalProducts,
    DateTime CreatedAt,
    Guid? ShopCategoryId = null,
    string? ShopCategoryName = null,
    string? BadgeText = null,
    string? BadgeColor = null,
    string? IconUrl = null,
    bool IsApproved = false,
    bool IsActive = false,
    string? Status = "Pending",
    string? BannerUrl = null,
    int MaxProductLimit = 200,
    string? TierName = null,
    decimal TierPriceUsd = 0);
