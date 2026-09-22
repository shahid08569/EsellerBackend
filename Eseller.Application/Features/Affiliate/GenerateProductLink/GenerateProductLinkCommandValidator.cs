using FluentValidation;

namespace Eseller.Application.Features.Affiliate.GenerateProductLink;

public class GenerateProductLinkCommandValidator
    : AbstractValidator<GenerateProductLinkCommand>
{
    public GenerateProductLinkCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}