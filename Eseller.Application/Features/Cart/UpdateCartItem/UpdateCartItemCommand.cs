using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cart.UpdateCartItem;

public record UpdateCartItemCommand(
    Guid ProductVariantId,
    int Quantity) : IRequest<Result>;