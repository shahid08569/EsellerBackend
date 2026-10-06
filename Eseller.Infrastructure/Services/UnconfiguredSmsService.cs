using Eseller.Application.Abstractions;

namespace Eseller.Infrastructure.Services;

/// <summary>
/// Fail-closed SMS when no real provider is wired. Prefer this over mock in Production.
/// </summary>
public sealed class UnconfiguredSmsService : ISmsService
{
    public bool IsMock => false;

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "SMS provider is not configured. Set a real ISmsService implementation or enable SmsSettings:UseMock only in Development.");

    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "SMS provider is not configured. Set a real ISmsService implementation or enable SmsSettings:UseMock only in Development.");
}
