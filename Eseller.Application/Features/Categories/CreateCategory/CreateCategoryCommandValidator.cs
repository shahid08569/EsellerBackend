using FluentValidation;

namespace Eseller.Application.Features.Categories.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(150);

        RuleFor(x => x.SeoTitle)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.SeoTitle));

        RuleFor(x => x.SeoDescription)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SeoDescription));
    }
}