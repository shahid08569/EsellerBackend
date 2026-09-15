using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Promotions;

public class FlashSale : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}