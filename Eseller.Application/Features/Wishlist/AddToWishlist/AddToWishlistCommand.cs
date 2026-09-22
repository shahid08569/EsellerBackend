using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Wishlist.AddToWishlist;

public record AddToWishlistCommand(Guid ProductId) : IRequest<Result<Guid>>;