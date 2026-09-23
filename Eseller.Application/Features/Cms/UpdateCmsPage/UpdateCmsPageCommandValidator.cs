using FluentValidation;

namespace Eseller.Application.Features.Cms.UpdateCmsPage;

public class UpdateCmsPageCommandValidator : AbstractValidator<UpdateCmsPageCommand>
{
    public UpdateCmsPageCommandValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().MaximumLength(300);

        RuleFor(x => x.Content)
            .NotEmpty();
    }
}