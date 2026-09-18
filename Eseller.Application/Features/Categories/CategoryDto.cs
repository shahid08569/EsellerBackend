namespace Eseller.Application.Features.Categories;

public record CategoryDto(
    Guid Id,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    string? ImageUrl,
    string? SeoTitle,
    string? SeoDescription,
    bool IsActive,
    DateTime CreatedAt);