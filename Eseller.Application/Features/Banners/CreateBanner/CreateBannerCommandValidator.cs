using FluentValidation;

namespace Eseller.Application.Features.Banners.CreateBanner;

public class CreateBannerCommandValidator : AbstractValidator<CreateBannerCommand>
{
    public CreateBannerCommandValidator()
    {
        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required.")
            .MaximumLength(500);

        RuleFor(x => x.LinkUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl));

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("End date must be after start date.");
    }
}