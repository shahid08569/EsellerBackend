using Eseller.Domain.Enums;
using FluentValidation;

namespace Eseller.Application.Features.Coupons.CreateCoupon;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Coupon code is required.")
            .MaximumLength(50)
            .Matches("^[A-Z0-9_-]+$").WithMessage("Code must be uppercase alphanumeric.");

        RuleFor(x => x.Value)
            .GreaterThan(0).WithMessage("Discount value must be greater than 0.");

        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100)
            .When(x => x.Type == DiscountType.Percentage)
            .WithMessage("Percentage discount cannot exceed 100.");

        RuleFor(x => x.MinOrderAmount)
            .GreaterThan(0).When(x => x.MinOrderAmount.HasValue)
            .WithMessage("Minimum order amount must be greater than 0.");

        RuleFor(x => x.MaxDiscount)
            .GreaterThan(0).When(x => x.MaxDiscount.HasValue)
            .WithMessage("Maximum discount must be greater than 0.");

        RuleFor(x => x.UsageLimit)
            .GreaterThan(0).When(x => x.UsageLimit.HasValue)
            .WithMessage("Usage limit must be greater than 0.");

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(DateTime.UtcNow).When(x => x.ExpiryDate.HasValue)
            .WithMessage("Expiry date must be in the future.");

        // Restricted scope requires at least one target
        RuleFor(x => x)
            .Must(x => x.ScopeType == CouponScopeType.Platform
                    || (x.ProductIds != null && x.ProductIds.Count > 0)
                    || (x.CategoryIds != null && x.CategoryIds.Count > 0)
                    || (x.ShopIds != null && x.ShopIds.Count > 0)
                    || (x.CustomerIds != null && x.CustomerIds.Count > 0))
            .WithMessage("Restricted coupons must target at least one product, category, shop, or customer.");
    }
}