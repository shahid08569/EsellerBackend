using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class Product : AuditableEntity, ISoftDelete
{
    public Guid ShopId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsApproved { get; set; }
    public int ViewCount { get; set; }
    public decimal? AvgRating { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}