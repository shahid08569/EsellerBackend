using FluentValidation;

namespace Eseller.Application.Features.Categories.UpdateCategory;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(150);

        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
    }
}