using FluentValidation;

namespace Eseller.Application.Features.Coupons.UpdateCoupon;

public class UpdateCouponCommandValidator : AbstractValidator<UpdateCouponCommand>
{
    public UpdateCouponCommandValidator()
    {
        RuleFor(x => x.CouponId).NotEmpty();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.MinOrderAmount).GreaterThan(0).When(x => x.MinOrderAmount.HasValue);
        RuleFor(x => x.MaxDiscount).GreaterThan(0).When(x => x.MaxDiscount.HasValue);
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue);
    }
}