using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class ShopTraffic : AuditableEntity
{
    public Guid ShopId { get; set; }
    public DateTime Date { get; set; }
    public int VisitCount { get; set; }
    public int UniqueVisitors { get; set; }
    public bool IsManual { get; set; }
    public string? Note { get; set; }

    // Navigation
    public Shop? Shop { get; set; }
}
