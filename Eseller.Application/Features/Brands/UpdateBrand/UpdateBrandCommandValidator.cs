using FluentValidation;

namespace Eseller.Application.Features.Brands.UpdateBrand;

public class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty().WithMessage("Brand ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Brand name is required.")
            .MaximumLength(150);
    }
}