using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.DeleteShop;

public record DeleteShopCommand(Guid ShopId) : IRequest<Result>;