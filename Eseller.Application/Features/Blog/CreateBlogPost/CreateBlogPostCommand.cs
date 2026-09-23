using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Blog.CreateBlogPost;

public record CreateBlogPostCommand(
    Guid? BlogCategoryId,
    string Title,
    string Content,
    string? CoverImageUrl,
    bool IsPublished,
    string? SeoTitle,
    string? SeoDescription) : IRequest<Result<Guid>>;