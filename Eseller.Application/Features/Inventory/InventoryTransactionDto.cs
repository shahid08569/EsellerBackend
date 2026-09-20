namespace Eseller.Application.Features.Inventory;

public record InventoryTransactionDto(
    Guid Id,
    Guid ProductVariantId,
    string TransactionType,
    int Quantity,
    int PreviousQuantity,
    int NewQuantity,
    string? ReferenceType,
    Guid? ReferenceId,
    Guid? CreatedByAccountId,
    DateTime CreatedAt);