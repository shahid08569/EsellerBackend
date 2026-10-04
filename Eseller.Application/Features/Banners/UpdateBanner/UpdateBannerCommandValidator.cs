using FluentValidation;

namespace Eseller.Application.Features.Banners.UpdateBanner;

public class UpdateBannerCommandValidator : AbstractValidator<UpdateBannerCommand>
{
    public UpdateBannerCommandValidator()
    {
        RuleFor(x => x.BannerId).NotEmpty();

        RuleFor(x => x.ImageUrl)
            .NotEmpty().MaximumLength(500);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Headline title is required.")
            .MaximumLength(200);

        RuleFor(x => x.Subtitle).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Subtitle));
        RuleFor(x => x.Description).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.ButtonText).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.ButtonText));
        RuleFor(x => x.BackgroundColor).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.BackgroundColor));
        RuleFor(x => x.TextColor).MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.TextColor));
        RuleFor(x => x.AvailableSizes).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.AvailableSizes));

        RuleFor(x => x.LinkUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl));

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}