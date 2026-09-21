using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Wishlist.ClearWishlist;

public record ClearWishlistCommand : IRequest<Result>;