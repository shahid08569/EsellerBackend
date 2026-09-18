namespace Eseller.Application.Features.Brands;

public record BrandDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    bool IsActive,
    DateTime CreatedAt);