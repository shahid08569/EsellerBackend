using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.ShippingConfig;

public class ShippingZone : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Regions { get; set; } // JSON
}