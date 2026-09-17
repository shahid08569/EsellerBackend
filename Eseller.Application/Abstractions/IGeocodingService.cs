namespace Eseller.Application.Abstractions;

public interface IGeocodingService
{
    Task<GeocodingResult?> ReverseGeocodeAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default);
}

public record GeocodingResult(
    string? City,
    string? Country,
    string? CountryCode);