using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class Address : AuditableEntity, ISoftDelete
{
    public Guid UserId { get; set; }
    public string Label { get; set; } = string.Empty;      // "Home", "Office"
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}