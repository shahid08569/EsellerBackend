namespace Eseller.Application.Features.Cms;

public record CmsPageDto(
    Guid Id,
    string Slug,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);