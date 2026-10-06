using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Notifications;

/// <summary>
/// Tracks active out-of-stock alerts so Super Admin is notified once per stock-out cycle.
/// </summary>
public class ProductStockAlert : BaseEntity
{
    public Guid ProductId { get; set; }
    public string AlertType { get; set; } = "OutOfStock";
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
