using FluentValidation;

namespace Eseller.Application.Features.Cart.UpdateCartItem;

public class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(x => x.ProductVariantId)
            .NotEmpty().WithMessage("Product variant is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100 per item.");
    }
}