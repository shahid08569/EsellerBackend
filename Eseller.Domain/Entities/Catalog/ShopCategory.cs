using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class ShopCategory : AuditableEntity, ISoftDelete
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BadgeText { get; set; }
    public string? BadgeColor { get; set; }
    public string? IconUrl { get; set; }
    public int DisplayOrder { get; set; } = 0;
    /// <summary>USD price for this seller tier package.</summary>
    public decimal PriceUsd { get; set; }
    /// <summary>Max approved+pending catalog listings allowed for shops on this tier.</summary>
    public int MaxProductListings { get; set; } = 200;
    /// <summary>When true, seller upgrade modal shows Recommended on this package (at most one).</summary>
    public bool IsRecommended { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}
