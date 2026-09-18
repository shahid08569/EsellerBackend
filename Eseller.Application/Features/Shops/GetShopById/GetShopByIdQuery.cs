using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.GetShopById;

public record GetShopByIdQuery(Guid ShopId) : IRequest<Result<ShopDetailDto>>;