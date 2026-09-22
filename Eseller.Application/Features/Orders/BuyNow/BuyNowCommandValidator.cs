using FluentValidation;

namespace Eseller.Application.Features.Orders.BuyNow;

public class BuyNowCommandValidator : AbstractValidator<BuyNowCommand>
{
    public BuyNowCommandValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty();

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100.");
    }
}