namespace Eseller.Infrastructure.Settings;

public class GeocodingSettings
{
    public const string SectionName = "GeocodingSettings";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-bdc.net/data/reverse-geocode";
}