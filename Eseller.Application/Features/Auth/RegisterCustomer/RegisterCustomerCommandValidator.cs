using FluentValidation;

namespace Eseller.Application.Features.Auth.RegisterCustomer;

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(150);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .MaximumLength(30);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m);

        RuleFor(x => x.DeviceType)
            .Must(dt =>
            {
                if (string.IsNullOrWhiteSpace(dt)) return true; // defaulted server-side
                var v = dt.Trim();
                return v.Equals("Desktop", StringComparison.OrdinalIgnoreCase)
                    || v.Equals("Mobile", StringComparison.OrdinalIgnoreCase)
                    || v.Equals("Tablet", StringComparison.OrdinalIgnoreCase);
            })
            .WithMessage("DeviceType must be Desktop, Mobile, or Tablet.");
    }
}