using Eseller.Application.Abstractions;
using Eseller.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Eseller.Infrastructure.Services;

public class BigDataCloudGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly GeocodingSettings _settings;
    private readonly ILogger<BigDataCloudGeocodingService> _logger;

    public BigDataCloudGeocodingService(
        HttpClient httpClient,
        IOptions<GeocodingSettings> settings,
        ILogger<BigDataCloudGeocodingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<GeocodingResult?> ReverseGeocodeAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_settings.BaseUrl}?latitude={latitude}&longitude={longitude}&localityLanguage=en&key={_settings.ApiKey}";

            var response = await _httpClient.GetFromJsonAsync<BigDataCloudResponse>(url, cancellationToken);

            if (response is null)
            {
                _logger.LogWarning("Geocoding returned null for {Lat},{Lng}", latitude, longitude);
                return null;
            }

            return new GeocodingResult(
                response.City ?? response.Locality,
                response.CountryName,
                response.CountryCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocoding failed for {Lat},{Lng}", latitude, longitude);
            return null;
        }
    }

    private class BigDataCloudResponse
    {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("locality")]
        public string? Locality { get; set; }

        [JsonPropertyName("countryName")]
        public string? CountryName { get; set; }

        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }
    }
}