using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.ShippingConfig;

public class ShippingMethod : AuditableEntity
{
    public Guid? ShopId { get; set; }
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public bool IsFree { get; set; }
    public bool IsActive { get; set; } = true;
}