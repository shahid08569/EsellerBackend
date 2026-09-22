using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cart.AddToCart;

public record AddToCartCommand(
    Guid ProductVariantId,
    int Quantity) : IRequest<Result<Guid>>;