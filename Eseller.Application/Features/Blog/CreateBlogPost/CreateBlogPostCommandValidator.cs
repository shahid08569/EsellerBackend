using FluentValidation;

namespace Eseller.Application.Features.Blog.CreateBlogPost;

public class CreateBlogPostCommandValidator : AbstractValidator<CreateBlogPostCommand>
{
    public CreateBlogPostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.");

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl));

        RuleFor(x => x.SeoTitle)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.SeoTitle));

        RuleFor(x => x.SeoDescription)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SeoDescription));
    }
}