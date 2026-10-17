using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class ProductGroupListing : AuditableEntity, ISoftDelete
{
    public Guid ShopId { get; set; }
    public string BatchName { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int DeliveredCount { get; set; }
    public string? CategoryFilter { get; set; }
    public string? BrandFilter { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}
