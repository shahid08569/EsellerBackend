using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Blog.GetBlogPostBySlug;

public class GetBlogPostBySlugQueryHandler
    : IRequestHandler<GetBlogPostBySlugQuery, Result<BlogPostDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBlogPostBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BlogPostDto>> Handle(
        GetBlogPostBySlugQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return Result.Failure<BlogPostDto>("Slug is required.", "INVALID_SLUG");

        var slug = request.Slug.Trim().ToLowerInvariant();

        var post = await _context.BlogPosts
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, cancellationToken);

        if (post is null)
            return Result.Failure<BlogPostDto>("Blog post not found.", "POST_NOT_FOUND");

        string? categoryName = null;
        string? categorySlug = null;

        if (post.BlogCategoryId.HasValue)
        {
            var category = await _context.BlogCategories
                .FirstOrDefaultAsync(c => c.Id == post.BlogCategoryId.Value, cancellationToken);

            categoryName = category?.Name;
            categorySlug = category?.Slug;
        }

        var dto = new BlogPostDto(
            post.Id,
            post.BlogCategoryId,
            categoryName,
            categorySlug,
            post.Title,
            post.Slug,
            post.Content,
            post.CoverImageUrl,
            post.IsPublished,
            post.PublishedAt,
            post.SeoTitle,
            post.SeoDescription,
            post.CreatedAt,
            post.UpdatedAt);

        return Result.Success(dto);
    }
}