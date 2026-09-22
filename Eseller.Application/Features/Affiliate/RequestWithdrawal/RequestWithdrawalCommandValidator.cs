using FluentValidation;

namespace Eseller.Application.Features.Affiliate.RequestWithdrawal;

public class RequestWithdrawalCommandValidator
    : AbstractValidator<RequestWithdrawalCommand>
{
    public RequestWithdrawalCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.PayoutMethod)
            .NotEmpty().WithMessage("Payout method is required.")
            .MaximumLength(50);
    }
}