using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cart.ClearCart;

public record ClearCartCommand : IRequest<Result>;