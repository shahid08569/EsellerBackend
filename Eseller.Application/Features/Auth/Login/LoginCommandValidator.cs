using FluentValidation;

namespace Eseller.Application.Features.Auth.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage("Username or email is required.")
            .MaximumLength(150).WithMessage("Username or email cannot exceed 150 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters.");
        RuleFor(x => x.Latitude)
    .InclusiveBetween(-90m, 90m)
    .WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
            .WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x.DeviceType)
            .NotEmpty()
            .Must(dt => dt == "Desktop" || dt == "Mobile" || dt == "Tablet")
            .WithMessage("Device type must be Desktop, Mobile, or Tablet.");
    }
}