namespace Eseller.Application.Features.Blog;

public record BlogPostListDto(
    Guid Id,
    Guid? BlogCategoryId,
    string? BlogCategoryName,
    string Title,
    string Slug,
    string? CoverImageUrl,
    bool IsPublished,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    string? Content = null,
    string? SeoDescription = null);