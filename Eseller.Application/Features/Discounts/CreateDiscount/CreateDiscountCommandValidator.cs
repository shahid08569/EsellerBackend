using Eseller.Domain.Enums;
using FluentValidation;

namespace Eseller.Application.Features.Discounts.CreateDiscount;

public class CreateDiscountCommandValidator : AbstractValidator<CreateDiscountCommand>
{
    public CreateDiscountCommandValidator()
    {
        RuleFor(x => x.Value)
            .GreaterThan(0).WithMessage("Discount value must be greater than 0.");

        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100)
            .When(x => x.Type == DiscountType.Percentage)
            .WithMessage("Percentage discount cannot exceed 100.");

        RuleFor(x => x.StartDate)
            .LessThan(x => x.EndDate).WithMessage("Start date must be before end date.");

        // Product scope — product_id required
        RuleFor(x => x.ProductId)
            .NotNull().WithMessage("Product is required for product-scoped discounts.")
            .When(x => x.ScopeType == DiscountScopeType.Product);

        // Category / Shop scope — scope_id required
        RuleFor(x => x.ScopeId)
            .NotNull().WithMessage("Scope ID is required.")
            .When(x => x.ScopeType == DiscountScopeType.Category
                    || x.ScopeType == DiscountScopeType.Shop);

        // Platform scope — scope_id must be null
        RuleFor(x => x.ScopeId)
            .Null().WithMessage("Scope ID should be null for platform-wide discounts.")
            .When(x => x.ScopeType == DiscountScopeType.Platform);
    }
}