using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Wishlist.GetWishlist;

public record GetWishlistQuery : IRequest<Result<IReadOnlyList<WishlistItemDto>>>;