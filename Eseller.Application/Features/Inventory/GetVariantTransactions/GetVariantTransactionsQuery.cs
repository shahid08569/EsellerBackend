using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Inventory.GetVariantTransactions;

public record GetVariantTransactionsQuery(
    Guid ProductId,
    Guid VariantId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<InventoryTransactionDto>>>;