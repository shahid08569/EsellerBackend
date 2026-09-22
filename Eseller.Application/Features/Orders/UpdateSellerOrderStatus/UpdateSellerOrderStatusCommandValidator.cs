using FluentValidation;

namespace Eseller.Application.Features.Orders.UpdateSellerOrderStatus;

public class UpdateSellerOrderStatusCommandValidator
    : AbstractValidator<UpdateSellerOrderStatusCommand>
{
    public UpdateSellerOrderStatusCommandValidator()
    {
        RuleFor(x => x.SellerOrderId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}