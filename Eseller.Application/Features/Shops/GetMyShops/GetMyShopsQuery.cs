using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.GetMyShops;

public record GetMyShopsQuery : IRequest<Result<IReadOnlyList<ShopListDto>>>;