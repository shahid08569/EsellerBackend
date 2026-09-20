using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Coupons.GetCouponUsages;

public class GetCouponUsagesQueryHandler
    : IRequestHandler<GetCouponUsagesQuery, Result<PagedList<CouponUsageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCouponUsagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<CouponUsageDto>>> Handle(
        GetCouponUsagesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<CouponUsageDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == request.CouponId, cancellationToken);

        if (coupon is null)
            return Result.Failure<PagedList<CouponUsageDto>>(
                "Coupon not found.", "COUPON_NOT_FOUND");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin && coupon.CreatedByAccountId != _currentUserService.AccountId.Value)
            return Result.Failure<PagedList<CouponUsageDto>>(
                "You do not have permission.", "FORBIDDEN");

        var query = _context.CouponUsages
            .Where(cu => cu.CouponId == request.CouponId)
            .OrderByDescending(cu => cu.UsedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var usages = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(cu => new CouponUsageDto(
                cu.Id,
                cu.CouponId,
                coupon.Code,
                cu.OrderRequestId,
                cu.UserId,
                _context.Users.Where(u => u.Id == cu.UserId).Select(u => u.Name).FirstOrDefault() ?? string.Empty,
                cu.UsedAt))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CouponUsageDto>(
            usages, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}