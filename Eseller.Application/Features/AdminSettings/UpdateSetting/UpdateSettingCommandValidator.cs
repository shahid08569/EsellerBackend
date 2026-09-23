using FluentValidation;

namespace Eseller.Application.Features.AdminSettings.UpdateSetting;

public class UpdateSettingCommandValidator : AbstractValidator<UpdateSettingCommand>
{
    public UpdateSettingCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Key is required.")
            .MaximumLength(100);

        RuleFor(x => x.Value)
            .NotNull().WithMessage("Value is required.");
    }
}