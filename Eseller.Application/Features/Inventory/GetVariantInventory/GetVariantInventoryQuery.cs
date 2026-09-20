using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Inventory.GetVariantInventory;

public record GetVariantInventoryQuery(
    Guid ProductId,
    Guid VariantId) : IRequest<Result<VariantInventoryDto>>;

public record VariantInventoryDto(
    Guid VariantId,
    string Sku,
    int StockQty,
    int LowStockThreshold,
    bool IsLowStock);