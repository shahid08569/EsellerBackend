using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Wishlist.RemoveFromWishlist;

public record RemoveFromWishlistCommand(Guid ProductId) : IRequest<Result>;