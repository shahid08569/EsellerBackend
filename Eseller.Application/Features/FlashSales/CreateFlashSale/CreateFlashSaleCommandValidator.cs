using FluentValidation;

namespace Eseller.Application.Features.FlashSales.CreateFlashSale;

public class CreateFlashSaleCommandValidator : AbstractValidator<CreateFlashSaleCommand>
{
    public CreateFlashSaleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate)
            .WithMessage("Start date must be before end date.");

        RuleFor(x => x.EndDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("End date must be in the future.");

        RuleFor(x => x.Products)
            .NotEmpty().WithMessage("At least one product is required.");

        RuleForEach(x => x.Products).ChildRules(p =>
        {
            p.RuleFor(x => x.ProductId).NotEmpty();
            p.RuleFor(x => x.DiscountType)
                .InclusiveBetween(1, 2)
                .WithMessage("DiscountType must be 1 (Percentage) or 2 (Fixed).");
            p.RuleFor(x => x.DiscountValue)
                .GreaterThan(0)
                .WithMessage("DiscountValue must be greater than 0.");
            p.RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100)
                .When(x => x.DiscountType == 1)
                .WithMessage("Percentage cannot exceed 100.");
        });
    }
}