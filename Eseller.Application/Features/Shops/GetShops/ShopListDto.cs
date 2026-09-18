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
    DateTime CreatedAt);