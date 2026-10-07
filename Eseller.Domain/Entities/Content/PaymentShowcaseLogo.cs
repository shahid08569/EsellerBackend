using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Content;

/// <summary>
/// Homepage payment-method logo for the storefront showcase marquee (SuperAdmin-managed).
/// </summary>
public class PaymentShowcaseLogo : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
