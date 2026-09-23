namespace Eseller.Application.Features.Banners;

public record HomepageBannerDto(
    Guid Id,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    bool IsCurrentlyActive,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);