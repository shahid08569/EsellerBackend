using FluentValidation;

namespace Eseller.Application.Features.AdminPartners.UpdatePartner;

public class UpdatePartnerCommandValidator : AbstractValidator<UpdatePartnerCommand>
{
    public UpdatePartnerCommandValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty();
    }
}