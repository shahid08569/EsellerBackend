using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Catalog;

public class InventoryTransaction : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    public InventoryTransactionType TransactionType { get; set; }
    public int Quantity { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? CreatedByAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
}