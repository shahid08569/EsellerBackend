using FluentValidation;

namespace Eseller.Application.Features.Products.RejectProduct;

public class RejectProductCommandValidator : AbstractValidator<RejectProductCommand>
{
    public RejectProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}