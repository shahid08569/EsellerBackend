using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Admin.Shopkeepers.RejectShopkeeper;

public record RejectShopkeeperCommand(
    Guid ShopkeeperId,
    string Reason) : IRequest<Result>;