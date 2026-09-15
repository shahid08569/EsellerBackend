using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class ProductVariant : AuditableEntity, ISoftDelete
{
    public Guid ProductId { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQty { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public decimal Price { get; set; }
    public string Sku { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}