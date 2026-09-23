using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Blog.DeleteBlogPost;

public class DeleteBlogPostCommandHandler
    : IRequestHandler<DeleteBlogPostCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteBlogPostCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        DeleteBlogPostCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can delete blog posts.", "FORBIDDEN");

        var post = await _context.BlogPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post is null)
            return Result.Failure("Blog post not found.", "POST_NOT_FOUND");

        _context.BlogPosts.Remove(post);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}