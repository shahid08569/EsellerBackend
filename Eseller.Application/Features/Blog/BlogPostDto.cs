namespace Eseller.Application.Features.Blog;

public record BlogPostDto(
    Guid Id,
    Guid? BlogCategoryId,
    string? BlogCategoryName,
    string? BlogCategorySlug,
    string Title,
    string Slug,
    string Content,
    string? CoverImageUrl,
    bool IsPublished,
    DateTime? PublishedAt,
    string? SeoTitle,
    string? SeoDescription,
    DateTime CreatedAt,
    DateTime UpdatedAt);