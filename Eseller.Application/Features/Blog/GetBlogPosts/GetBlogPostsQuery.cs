using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Blog.GetBlogPosts;

public record GetBlogPostsQuery(
    Guid? CategoryId = null,
    bool? IsPublished = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<BlogPostListDto>>>;