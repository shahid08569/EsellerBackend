using FluentValidation;

namespace Eseller.Application.Features.Admin.Shopkeepers.RejectShopkeeper;

public class RejectShopkeeperCommandValidator : AbstractValidator<RejectShopkeeperCommand>
{
    public RejectShopkeeperCommandValidator()
    {
        RuleFor(x => x.ShopkeeperId)
            .NotEmpty().WithMessage("Shopkeeper ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}