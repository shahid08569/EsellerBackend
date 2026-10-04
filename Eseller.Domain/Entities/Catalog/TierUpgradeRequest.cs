using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class TierUpgradeRequest : AuditableEntity
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string CurrentTier { get; set; } = "Bronze (Free)";
    public string RequestedTier { get; set; } = string.Empty;
    public Guid? RequestedCategoryId { get; set; }
    public string? BadgeText { get; set; }
    public decimal Price { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNote { get; set; }
    public string? ReceiptUrl { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
