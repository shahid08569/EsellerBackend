using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cart.GetCart;

public record GetCartQuery : IRequest<Result<CartDto>>;