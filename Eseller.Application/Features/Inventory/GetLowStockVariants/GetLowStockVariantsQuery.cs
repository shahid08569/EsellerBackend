using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Inventory.GetLowStockVariants;

public record GetLowStockVariantsQuery(
    Guid ShopId) : IRequest<Result<IReadOnlyList<LowStockVariantDto>>>;