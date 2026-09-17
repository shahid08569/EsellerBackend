namespace Eseller.Application.Abstractions;

public interface ILocationService
{
    Task<bool> IsValidLocationAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default);

    Task LogLocationAsync(
        Guid? accountId,
        decimal latitude,
        decimal longitude,
        string deviceType,
        string action,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}