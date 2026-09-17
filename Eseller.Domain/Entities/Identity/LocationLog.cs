using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class LocationLog : BaseEntity
{
    public Guid? AccountId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? City { get; set; }              // ← NEW
    public string? Country { get; set; }           // ← NEW
    public string? CountryCode { get; set; }       // ← NEW (e.g. "PK")
    public string DeviceType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}