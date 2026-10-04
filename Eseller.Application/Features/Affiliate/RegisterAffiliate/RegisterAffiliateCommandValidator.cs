using FluentValidation;

namespace Eseller.Application.Features.Affiliate.RegisterAffiliate;

public class RegisterAffiliateCommandValidator : AbstractValidator<RegisterAffiliateCommand>
{
    public RegisterAffiliateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");

        // ✅ Location validation
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("Invalid latitude.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("Invalid longitude.");

        RuleFor(x => x.DeviceType)
            .NotEmpty().WithMessage("Device type is required.")
            .MaximumLength(50);
    }
}