using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.CreateReview;

public class CreateReviewCommandHandler
    : IRequestHandler<CreateReviewCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure<Guid>("Only customers can write reviews.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        // Verify product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // If OrderRequestItemId provided, verify ownership
        if (request.OrderRequestItemId.HasValue)
        {
            var orderItem = await _context.OrderRequestItems
                .FirstOrDefaultAsync(i => i.Id == request.OrderRequestItemId.Value,
                    cancellationToken);

            if (orderItem is null)
                return Result.Failure<Guid>("Order item not found.", "ORDER_ITEM_NOT_FOUND");

            var order = await _context.OrderRequests
                .FirstOrDefaultAsync(o => o.Id == orderItem.OrderRequestId && o.UserId == user.Id,
                    cancellationToken);

            if (order is null)
                return Result.Failure<Guid>(
                    "You can only review your own purchased items.",
                    "NOT_YOUR_ORDER");

            if (order.Status != OrderStatus.Delivered)
                return Result.Failure<Guid>(
                    "You can only review after the order is delivered.",
                    "ORDER_NOT_DELIVERED");

            // Duplicate check
            var duplicate = await _context.Reviews
                .AnyAsync(r => r.UserId == user.Id
                            && r.OrderRequestItemId == request.OrderRequestItemId.Value,
                    cancellationToken);

            if (duplicate)
                return Result.Failure<Guid>(
                    "You have already reviewed this item.",
                    "ALREADY_REVIEWED");
        }
        else
        {
            // One review per user per product when not tied to an order line
            var already = await _context.Reviews
                .AnyAsync(r => r.UserId == user.Id && r.ProductId == request.ProductId,
                    cancellationToken);
            if (already)
                return Result.Failure<Guid>(
                    "You have already reviewed this product.",
                    "ALREADY_REVIEWED");
        }

        if (request.Rating < 1 || request.Rating > 5)
            return Result.Failure<Guid>("Rating must be between 1 and 5.", "VALIDATION_ERROR");

        var review = new Review
        {
            ProductId = request.ProductId,
            UserId = user.Id,
            OrderRequestItemId = request.OrderRequestItemId,
            Rating = request.Rating,
            Title = request.Title,
            Comment = request.Comment,
            IsApproved = true, // Live immediately so storefront rating updates dynamically
            CreatedAt = _dateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        await RecalculateAvgRatingAsync(request.ProductId, cancellationToken);

        return Result.Success(review.Id);
    }

    private async Task RecalculateAvgRatingAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null) return;

        var approved = await _context.Reviews
            .Where(r => r.ProductId == productId && r.IsApproved)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        product.AvgRating = approved.Count == 0
            ? null
            : (decimal)approved.Average();
        product.SetUpdatedAt(_dateTime.UtcNow);

        // Keep catalog twin / sibling listings in sync when possible
        if (product.SourceProductId.HasValue)
        {
            var catalog = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == product.SourceProductId.Value, cancellationToken);
            if (catalog is not null)
            {
                catalog.AvgRating = product.AvgRating;
                catalog.SetUpdatedAt(_dateTime.UtcNow);
            }
        }
        else
        {
            var listings = await _context.Products
                .Where(p => p.SourceProductId == productId)
                .ToListAsync(cancellationToken);
            foreach (var listing in listings)
            {
                listing.AvgRating = product.AvgRating;
                listing.SetUpdatedAt(_dateTime.UtcNow);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}