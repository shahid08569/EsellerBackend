using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.GetPendingReviews;

public class GetPendingReviewsQueryHandler
    : IRequestHandler<GetPendingReviewsQuery, Result<PagedList<ReviewDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingReviewsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ReviewDto>>> Handle(
        GetPendingReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ReviewDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<ReviewDto>>(
                "Only admins can view pending reviews.", "FORBIDDEN");

        var query = _context.Reviews
            .Where(r => !r.IsApproved);

        var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
        if (partnerShopIds != null)
        {
            var permittedProductIds = _context.Products.IgnoreQueryFilters().AsNoTracking()
                .Where(p => partnerShopIds.Contains(p.ShopId) && p.DeletedAt == null)
                .Select(p => p.Id);
            query = query.Where(r => permittedProductIds.Contains(r.ProductId));
        }

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