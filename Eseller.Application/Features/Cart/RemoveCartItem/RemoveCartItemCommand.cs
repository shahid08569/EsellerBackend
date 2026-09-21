using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cart.RemoveCartItem;

public record RemoveCartItemCommand(Guid ProductVariantId) : IRequest<Result>;