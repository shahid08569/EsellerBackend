using FluentValidation;

namespace Eseller.Application.Features.Auth.RegisterSeller;

public class RegisterSellerCommandValidator : AbstractValidator<RegisterSellerCommand>
{
    public RegisterSellerCommandValidator()
    {
        // Personal
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

        // Shop
        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("Store name is required.")
            .MaximumLength(200);

        RuleFor(x => x.StoreUrl)
            .NotEmpty().WithMessage("Store URL is required.")
            .MaximumLength(250)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Store URL can only contain lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.StoreDescription)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.StoreDescription));

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500);

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required.")
            .MaximumLength(100);

        // Location
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m);

        RuleFor(x => x.DeviceType)
            .NotEmpty()
            .Must(dt => dt == "Desktop" || dt == "Mobile" || dt == "Tablet");
    }
}