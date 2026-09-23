using FluentValidation;

namespace Eseller.Application.Features.Blog.CreateBlogCategory;

public class CreateBlogCategoryCommandValidator : AbstractValidator<CreateBlogCategoryCommand>
{
    public CreateBlogCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150);
    }
}