using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Reviews.GetAdminReviews;

public class GetAdminReviewsQueryHandler
    : IRequestHandler<GetAdminReviewsQuery, Result<PagedList<ReviewDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAdminReviewsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ReviewDto>>> Handle(
        GetAdminReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ReviewDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<ReviewDto>>(
                "Only admins can manage reviews.", "FORBIDDEN");

        var status = (request.Status ?? "pending").Trim().ToLowerInvariant();

        var query = _context.Reviews.AsQueryable();

        query = status switch
        {
            "approved" or "published" or "kept" => query.Where(r => r.IsApproved),
            "all" => query,
            _ => query.Where(r => !r.IsApproved)
        };

        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var reviews = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var productIds = reviews.Select(r => r.ProductId).Distinct().ToList();

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.ShopId })
            .ToListAsync(cancellationToken);

        var shopIds = products.Select(p => p.ShopId).Distinct().ToList();
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var productMap = products.ToDictionary(p => p.Id);

        var result = reviews.Select(r =>
        {
            productMap.TryGetValue(r.ProductId, out var product);
            var shopName = product is not null
                ? shops.GetValueOrDefault(product.ShopId)
                : null;

            return new ReviewDto(
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
                r.CreatedAt,
                product?.Name,
                shopName);
        }).ToList();

        var pagedList = new PagedList<ReviewDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}
