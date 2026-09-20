using FluentValidation;

namespace Eseller.Application.Features.Products.UpdateProductImageSort;

public class UpdateProductImageSortCommandValidator
    : AbstractValidator<UpdateProductImageSortCommand>
{
    public UpdateProductImageSortCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ImageId).NotEmpty();
        RuleFor(x => x.NewSortOrder).GreaterThanOrEqualTo(1);
    }
}