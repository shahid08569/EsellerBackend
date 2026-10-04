namespace Eseller.Application.Features.Banners;

public record HomepageBannerDto(
    Guid Id,
    string ImageUrl,
    string? LinkUrl,
    string? Title,
    string? Subtitle,
    string? Description,
    decimal? Price,
    decimal? OriginalPrice,
    string? ButtonText,
    string? BackgroundColor,
    string? TextColor,
    string? AvailableSizes,
    int SortOrder,
    bool IsActive,
    bool IsCurrentlyActive,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);