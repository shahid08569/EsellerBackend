using FluentValidation;

namespace Eseller.Application.Features.ProductVariants.UpdateProductVariant;

public class UpdateProductVariantCommandValidator
    : AbstractValidator<UpdateProductVariantCommand>
{
    public UpdateProductVariantCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQty).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Attributes)
            .NotEmpty().WithMessage("At least one attribute is required.");

        RuleForEach(x => x.Attributes).ChildRules(attr =>
        {
            attr.RuleFor(a => a.AttributeName).NotEmpty().MaximumLength(100);
            attr.RuleFor(a => a.AttributeValue).NotEmpty().MaximumLength(200);
        });

        RuleFor(x => x.Attributes)
            .Must(attrs => attrs.Select(a => a.AttributeName.ToLowerInvariant()).Distinct().Count() == attrs.Count)
            .WithMessage("Duplicate attribute names are not allowed.");
    }
}