using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Inventory.AdjustStock;

public record AdjustStockCommand(
    Guid ProductId,
    Guid VariantId,
    int NewQuantity,
    string Reason) : IRequest<Result>;