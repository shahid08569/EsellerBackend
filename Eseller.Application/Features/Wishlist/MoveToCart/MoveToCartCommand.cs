using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Wishlist.MoveToCart;

public record MoveToCartCommand(
    Guid ProductId,
    Guid ProductVariantId,
    int Quantity) : IRequest<Result>;