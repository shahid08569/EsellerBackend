using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class Product : AuditableEntity, ISoftDelete
{
    public Guid ShopId { get; set; }
    /// <summary>
    /// When set, this product is a seller listing cloned from a Super Admin catalog product.
    /// </summary>
    public Guid? SourceProductId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsFeatured { get; set; }
    /// <summary>Curated homepage Hot Selling pin (Super Admin).</summary>
    public bool IsHotSelling { get; set; }
    public bool IsApproved { get; set; }
    /// <summary>
    /// Master Warehouse SKUs only: when true, product is visible on the customer website
    /// under Platform Master Warehouse. Off by default — SuperAdmin opts in.
    /// Seller shop listings ignore this flag (they use IsApproved + IsActive).
    /// </summary>
    public bool IsStorefrontLive { get; set; }
    public int ViewCount { get; set; }
    public decimal? AvgRating { get; set; }
    /// <summary>Approved review count (denormalized; kept in sync with AvgRating).</summary>
    public int ReviewCount { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? RejectionReason { get; private set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }

    public void Approve()
    {
        IsApproved = true;
        IsActive = true;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        IsApproved = false;
        IsActive = false;
        RejectionReason = reason;
    }

    public void ResetToPending()
    {
        IsApproved = false;
        IsActive = true;
        RejectionReason = null;
    }
}