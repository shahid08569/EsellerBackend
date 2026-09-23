using FluentValidation;

namespace Eseller.Application.Features.Blog.UpdateBlogPost;

public class UpdateBlogPostCommandValidator : AbstractValidator<UpdateBlogPostCommand>
{
    public UpdateBlogPostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().MaximumLength(300);

        RuleFor(x => x.Content)
            .NotEmpty();

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