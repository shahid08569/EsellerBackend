using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Blog.UpdateBlogPost;

public class UpdateBlogPostCommandHandler
    : IRequestHandler<UpdateBlogPostCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateBlogPostCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateBlogPostCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can update blog posts.", "FORBIDDEN");

        var post = await _context.BlogPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post is null)
            return Result.Failure("Blog post not found.", "POST_NOT_FOUND");

        if (request.BlogCategoryId.HasValue)
        {
            var categoryExists = await _context.BlogCategories
                .AnyAsync(c => c.Id == request.BlogCategoryId.Value, cancellationToken);

            if (!categoryExists)
                return Result.Failure("Category not found.", "CATEGORY_NOT_FOUND");
        }

        var now = _dateTime.UtcNow;

        post.BlogCategoryId = request.BlogCategoryId;
        post.Title = request.Title.Trim();
        post.Content = request.Content;
        post.CoverImageUrl = request.CoverImageUrl?.Trim();
        post.SeoTitle = request.SeoTitle?.Trim();
        post.SeoDescription = request.SeoDescription?.Trim();

        // Handle publish transition
        if (request.IsPublished && !post.IsPublished)
        {
            post.PublishedAt = now;
        }
        else if (!request.IsPublished)
        {
            post.PublishedAt = null;
        }

        post.IsPublished = request.IsPublished;
        post.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}