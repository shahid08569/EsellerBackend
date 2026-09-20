using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Coupons.GetCoupons;

public class GetCouponsQueryHandler
    : IRequestHandler<GetCouponsQuery, Result<PagedList<CouponListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public GetCouponsQueryHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<CouponListDto>>> Handle(
        GetCouponsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Coupons.AsQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(c => c.IsActive == request.IsActive.Value);

        query = query.OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var now = _dateTime.UtcNow;

        var coupons = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CouponListDto(
                c.Id,
                c.Code,
                c.Type.ToString(),
                c.Value,
                c.MinOrderAmount,
                c.MaxDiscount,
                c.ExpiryDate,
                c.UsageLimit,
                c.UsedCount,
                c.ScopeType.ToString(),
                c.IsActive,
                c.IsActive
                    && (c.ExpiryDate == null || c.ExpiryDate > now)
                    && (c.UsageLimit == null || c.UsedCount < c.UsageLimit)))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CouponListDto>(
            coupons, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}