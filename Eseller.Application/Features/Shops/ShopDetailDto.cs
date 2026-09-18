namespace Eseller.Application.Features.Shops;

public record ShopDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    string? Phone,
    string? Address,
    string? City,
    string? Country,
    decimal Rating,
    int TotalProducts,
    bool IsActive,
    DateTime CreatedAt);