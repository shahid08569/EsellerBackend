namespace Eseller.Application.Features.Blog;

public record BlogCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    int PostCount);