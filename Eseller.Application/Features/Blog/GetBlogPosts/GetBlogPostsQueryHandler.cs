using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Blog.GetBlogPosts;

public class GetBlogPostsQueryHandler
    : IRequestHandler<GetBlogPostsQuery, Result<PagedList<BlogPostListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBlogPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<BlogPostListDto>>> Handle(
        GetBlogPostsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.BlogPosts.AsQueryable();

        // Public — only published
        query = query.Where(p => p.IsPublished);

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.BlogCategoryId == request.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Title.ToLower().Contains(search) ||
                p.Content.ToLower().Contains(search));
        }

        query = query.OrderByDescending(p => p.PublishedAt ?? p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var categoryIds = posts
            .Where(p => p.BlogCategoryId.HasValue)
            .Select(p => p.BlogCategoryId!.Value)
            .Distinct()
            .ToList();

        var categories = await _context.BlogCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c, cancellationToken);

        var result = posts.Select(p =>
        {
            var category = p.BlogCategoryId.HasValue
                ? categories.GetValueOrDefault(p.BlogCategoryId.Value)
                : null;

            return new BlogPostListDto(
                p.Id,
                p.BlogCategoryId,
                category?.Name,
                p.Title,
                p.Slug,
                p.CoverImageUrl,
                p.IsPublished,
                p.PublishedAt,
                p.CreatedAt);
        }).ToList();

        var pagedList = new PagedList<BlogPostListDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}