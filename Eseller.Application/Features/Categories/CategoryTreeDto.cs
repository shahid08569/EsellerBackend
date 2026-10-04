namespace Eseller.Application.Features.Categories;

public record CategoryTreeDto(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl,
    List<CategoryTreeDto> Children,
    bool IsFeaturedOnHomepage = false,
    int HomepageDisplayOrder = 0);