using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.DeleteReview;

public class DeleteReviewCommandHandler
    : IRequestHandler<DeleteReviewCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        DeleteReviewCommand request,
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

        var productId = review.ProductId;
        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is not null)
        {
            var approved = await _context.Reviews
                .Where(r => r.ProductId == productId && r.IsApproved)
                .Select(r => r.Rating)
                .ToListAsync(cancellationToken);
            product.AvgRating = approved.Count == 0 ? null : (decimal)approved.Average();
            product.SetUpdatedAt(DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}