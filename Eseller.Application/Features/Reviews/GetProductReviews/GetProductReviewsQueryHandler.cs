using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.GetProductReviews;

public class GetProductReviewsQueryHandler
    : IRequestHandler<GetProductReviewsQuery, Result<PagedList<ReviewDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProductReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<ReviewDto>>> Handle(
        GetProductReviewsQuery request,
        CancellationToken cancellationToken)
    {
        // Verify product exists
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == request.ProductId && p.IsActive && p.IsApproved,
                cancellationToken);

        if (!productExists)
            return Result.Failure<PagedList<ReviewDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        // Only approved reviews in public
        var query = _context.Reviews
            .Where(r => r.ProductId == request.ProductId && r.IsApproved);

        if (request.Rating.HasValue)
            query = query.Where(r => r.Rating == request.Rating.Value);

        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var reviews = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var result = reviews.Select(r => new ReviewDto(
            r.Id,
            r.ProductId,
            r.UserId,
            users.GetValueOrDefault(r.UserId, string.Empty),
            r.OrderRequestItemId,
            r.OrderRequestItemId != null,
            r.Rating,
            r.Title,
            r.Comment,
            r.IsApproved,
            r.CreatedAt)).ToList();

        var pagedList = new PagedList<ReviewDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}