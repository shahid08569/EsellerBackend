using FluentValidation;

namespace Eseller.Application.Features.Wishlist.MoveToCart;

public class MoveToCartCommandValidator : AbstractValidator<MoveToCartCommand>
{
    public MoveToCartCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductVariantId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100.");
    }
}