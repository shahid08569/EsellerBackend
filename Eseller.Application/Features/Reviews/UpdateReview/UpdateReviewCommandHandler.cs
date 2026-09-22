using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.UpdateReview;

public class UpdateReviewCommandHandler
    : IRequestHandler<UpdateReviewCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        UpdateReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);

        if (review is null)
            return Result.Failure("Review not found.", "REVIEW_NOT_FOUND");

        // Ownership check
        if (!isAdmin)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || review.UserId != user.Id)
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        review.Rating = request.Rating;
        review.Title = request.Title;
        review.Comment = request.Comment;
        review.IsApproved = false;   // Re-approval after edit

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}