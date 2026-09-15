namespace Eseller.Domain.Enums;

public enum InventoryTransactionType
{
    Order = 1,
    Return = 2,
    ManualAdjustment = 3,
    Restock = 4,
    InitialStock = 5
}