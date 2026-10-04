using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Identity;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.Services;

public class LocationService : ILocationService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;
    private readonly IGeocodingService _geocodingService;
    private readonly ILogger<LocationService> _logger;

    public LocationService(
        IApplicationDbContext context,
        IDateTime dateTime,
        IGeocodingService geocodingService,
        ILogger<LocationService> logger)
    {
        _context = context;
        _dateTime = dateTime;
        _geocodingService = geocodingService;
        _logger = logger;
    }

    public Task<bool> IsValidLocationAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        if (latitude < -90 || latitude > 90) return Task.FromResult(false);
        if (longitude < -180 || longitude > 180) return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task LogLocationAsync(
        Guid? accountId,
        decimal latitude,
        decimal longitude,
        string deviceType,
        string action,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Reverse geocoding — fail-safe (agar fail ho toh null rahega)
        var geocoding = await _geocodingService.ReverseGeocodeAsync(
            latitude, longitude, cancellationToken);

        var log = new LocationLog
        {
            AccountId = accountId,
            Latitude = latitude,
            Longitude = longitude,
            City = geocoding?.City,
            Country = geocoding?.Country,
            CountryCode = geocoding?.CountryCode,
            DeviceType = deviceType,
            Action = action,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            RecordedAt = _dateTime.UtcNow
        };

        _context.LocationLogs.Add(log);
        
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Location logged for {AccountId}: {City}, {Country} ({Lat},{Lng}) - {Action}",
            accountId, geocoding?.City ?? "Unknown", geocoding?.Country ?? "Unknown",
            latitude, longitude, action);
    }
}