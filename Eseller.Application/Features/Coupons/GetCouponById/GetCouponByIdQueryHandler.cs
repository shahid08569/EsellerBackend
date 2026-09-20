using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Coupons.GetCouponById;

public class GetCouponByIdQueryHandler
    : IRequestHandler<GetCouponByIdQuery, Result<CouponDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public GetCouponByIdQueryHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<CouponDto>> Handle(
        GetCouponByIdQuery request,
        CancellationToken cancellationToken)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == request.CouponId, cancellationToken);

        if (coupon is null)
            return Result.Failure<CouponDto>("Coupon not found.", "COUPON_NOT_FOUND");

        var now = _dateTime.UtcNow;
        var isValid = coupon.IsActive
            && (coupon.ExpiryDate == null || coupon.ExpiryDate > now)
            && (coupon.UsageLimit == null || coupon.UsedCount < coupon.UsageLimit);

        var dto = new CouponDto(
            coupon.Id,
            coupon.Code,
            coupon.Type.ToString(),
            coupon.Value,
            coupon.MinOrderAmount,
            coupon.MaxDiscount,
            coupon.ExpiryDate,
            coupon.UsageLimit,
            coupon.UsedCount,
            coupon.ScopeType.ToString(),
            coupon.CreatedByAccountId,
            coupon.IsActive,
            isValid,
            coupon.CreatedAt);

        return Result.Success(dto);
    }
}