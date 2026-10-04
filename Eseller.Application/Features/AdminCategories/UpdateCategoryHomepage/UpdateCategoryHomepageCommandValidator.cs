using FluentValidation;

namespace Eseller.Application.Features.AdminCategories.UpdateCategoryHomepage;

public class UpdateCategoryHomepageCommandValidator
    : AbstractValidator<UpdateCategoryHomepageCommand>
{
    public UpdateCategoryHomepageCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();

        RuleFor(x => x.HomepageDisplayOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Display order must be 0 or greater.");
    }
}