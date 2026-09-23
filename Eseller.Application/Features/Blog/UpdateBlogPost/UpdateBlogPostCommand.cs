using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Blog.UpdateBlogPost;

public record UpdateBlogPostCommand(
    Guid PostId,
    Guid? BlogCategoryId,
    string Title,
    string Content,
    string? CoverImageUrl,
    bool IsPublished,
    string? SeoTitle,
    string? SeoDescription) : IRequest<Result>;