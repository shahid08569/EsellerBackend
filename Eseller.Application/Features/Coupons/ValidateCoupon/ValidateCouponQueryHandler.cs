using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Coupons.ValidateCoupon;

public class ValidateCouponQueryHandler
    : IRequestHandler<ValidateCouponQuery, Result<CouponValidationResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public ValidateCouponQueryHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<CouponValidationResult>> Handle(
        ValidateCouponQuery request,
        CancellationToken cancellationToken)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Code == request.Code.ToUpperInvariant(), cancellationToken);

        if (coupon is null)
            return Result.Failure<CouponValidationResult>("Coupon not found.", "COUPON_NOT_FOUND");

        // Validation checks
        if (!coupon.IsActive)
            return Result.Success(new CouponValidationResult(
                coupon.Id, coupon.Code, false, "Coupon is not active.", 0));

        var now = _dateTime.UtcNow;
        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < now)
            return Result.Success(new CouponValidationResult(
                coupon.Id, coupon.Code, false, "Coupon has expired.", 0));

        if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
            return Result.Success(new CouponValidationResult(
                coupon.Id, coupon.Code, false, "Coupon usage limit reached.", 0));

        if (coupon.MinOrderAmount.HasValue && request.OrderAmount < coupon.MinOrderAmount.Value)
            return Result.Success(new CouponValidationResult(
                coupon.Id, coupon.Code, false,
                $"Minimum order amount is {coupon.MinOrderAmount.Value}.", 0));

        // Calculate discount
        decimal discountAmount;
        if (coupon.Type == DiscountType.Percentage)
        {
            discountAmount = request.OrderAmount * (coupon.Value / 100m);
            if (coupon.MaxDiscount.HasValue && discountAmount > coupon.MaxDiscount.Value)
                discountAmount = coupon.MaxDiscount.Value;
        }
        else
        {
            discountAmount = coupon.Value;
        }

        // Ensure discount doesn't exceed order amount
        if (discountAmount > request.OrderAmount)
            discountAmount = request.OrderAmount;

        return Result.Success(new CouponValidationResult(
            coupon.Id, coupon.Code, true, null, discountAmount));
    }
}