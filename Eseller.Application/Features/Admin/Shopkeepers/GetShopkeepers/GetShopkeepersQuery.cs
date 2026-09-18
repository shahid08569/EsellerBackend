using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Admin.Shopkeepers.GetShopkeepers;

public record GetShopkeepersQuery(
    ShopkeeperApprovalStatus? Status) : IRequest<Result<IReadOnlyList<ShopkeeperDto>>>;