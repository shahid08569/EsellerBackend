using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.ApproveReview;

public class ApproveReviewCommandHandler
    : IRequestHandler<ApproveReviewCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApproveReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        ApproveReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can approve reviews.", "FORBIDDEN");

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);

        if (review is null)
            return Result.Failure("Review not found.", "REVIEW_NOT_FOUND");

        review.IsApproved = true;

        await _context.SaveChangesAsync(cancellationToken);

        // Update product average rating
        await UpdateProductAvgRating(review.ProductId, cancellationToken);

        return Result.Success();
    }

    private async Task UpdateProductAvgRating(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null) return;

        var approvedReviews = await _context.Reviews
            .Where(r => r.ProductId == productId && r.IsApproved)
            .ToListAsync(cancellationToken);

        if (approvedReviews.Count == 0)
        {
            product.AvgRating = null;
            product.ReviewCount = 0;
        }
        else
        {
            product.AvgRating = (decimal)approvedReviews.Average(r => r.Rating);
            product.ReviewCount = approvedReviews.Count;
        }

        product.SetUpdatedAt(_dateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);
    }
}