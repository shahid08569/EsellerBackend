using FluentValidation;

namespace Eseller.Application.Features.FlashSales.UpdateFlashSale;

public class UpdateFlashSaleCommandValidator : AbstractValidator<UpdateFlashSaleCommand>
{
    public UpdateFlashSaleCommandValidator()
    {
        RuleFor(x => x.FlashSaleId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate)
            .WithMessage("Start date must be before end date.");

        RuleFor(x => x.Products)
            .NotEmpty().WithMessage("At least one product is required.");

        RuleForEach(x => x.Products).ChildRules(p =>
        {
            p.RuleFor(x => x.ProductId).NotEmpty();
            p.RuleFor(x => x.DiscountType).InclusiveBetween(1, 2);
            p.RuleFor(x => x.DiscountValue).GreaterThan(0);
            p.RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100)
                .When(x => x.DiscountType == 1);
        });
    }
}