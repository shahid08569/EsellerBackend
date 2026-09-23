using FluentValidation;

namespace Eseller.Application.Features.Cms.CreateCmsPage;

public class CreateCmsPageCommandValidator : AbstractValidator<CreateCmsPageCommand>
{
    public CreateCmsPageCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(150)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase, alphanumeric, and hyphen-separated.");

        RuleFor(x => x.Title)
            .NotEmpty().MaximumLength(300);

        RuleFor(x => x.Content)
            .NotEmpty();
    }
}