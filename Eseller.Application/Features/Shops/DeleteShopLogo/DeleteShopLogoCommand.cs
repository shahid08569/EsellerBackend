using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.DeleteShopLogo;

public record DeleteShopLogoCommand(Guid ShopId) : IRequest<Result>;