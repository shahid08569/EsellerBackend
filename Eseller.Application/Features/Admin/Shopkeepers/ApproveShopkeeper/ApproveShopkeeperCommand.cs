using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Admin.Shopkeepers.ApproveShopkeeper;

public record ApproveShopkeeperCommand(Guid ShopkeeperId) : IRequest<Result>;