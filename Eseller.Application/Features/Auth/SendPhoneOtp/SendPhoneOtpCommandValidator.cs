using FluentValidation;

namespace Eseller.Application.Features.Auth.SendPhoneOtp;

public class SendPhoneOtpCommandValidator : AbstractValidator<SendPhoneOtpCommand>
{
    public SendPhoneOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(150);
    }
}