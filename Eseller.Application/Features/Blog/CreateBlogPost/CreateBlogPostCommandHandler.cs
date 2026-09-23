using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Content;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Blog.CreateBlogPost;

public class CreateBlogPostCommandHandler
    : IRequestHandler<CreateBlogPostCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateBlogPostCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateBlogPostCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>("Only admins can create blog posts.", "FORBIDDEN");

        if (request.BlogCategoryId.HasValue)
        {
            var categoryExists = await _context.BlogCategories
                .AnyAsync(c => c.Id == request.BlogCategoryId.Value, cancellationToken);

            if (!categoryExists)
                return Result.Failure<Guid>("Category not found.", "CATEGORY_NOT_FOUND");
        }

        var now = _dateTime.UtcNow;
        var baseSlug = GenerateSlug(request.Title);
        var slug = await EnsureUniqueSlug(baseSlug, null, cancellationToken);

        var post = new BlogPost
        {
            BlogCategoryId = request.BlogCategoryId,
            Title = request.Title.Trim(),
            Slug = slug,
            Content = request.Content,
            CoverImageUrl = request.CoverImageUrl?.Trim(),
            IsPublished = request.IsPublished,
            PublishedAt = request.IsPublished ? now : null,
            SeoTitle = request.SeoTitle?.Trim(),
            SeoDescription = request.SeoDescription?.Trim(),
            CreatedByAccountId = _currentUserService.AccountId.Value
        };

        post.SetCreatedAt(now);
        post.SetUpdatedAt(now);

        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(post.Id);
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant().Trim()
            .Replace(" ", "-")
            .Replace("_", "-");

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        return slug.Trim('-');
    }

    private async Task<string> EnsureUniqueSlug(
        string baseSlug,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var counter = 1;

        while (await _context.BlogPosts
            .AnyAsync(p => p.Slug == slug && (!excludeId.HasValue || p.Id != excludeId.Value),
                cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }
}