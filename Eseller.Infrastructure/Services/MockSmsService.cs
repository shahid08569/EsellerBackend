using Eseller.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.Services;

public class MockSmsService : ISmsService
{
    private readonly ILogger<MockSmsService> _logger;

    public bool IsMock => true;

    public MockSmsService(ILogger<MockSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // Never log message body (may contain OTP / PII)
        _logger.LogWarning("[MOCK SMS] To: {PhoneNumber} | length={Length}", phoneNumber, message?.Length ?? 0);
        return Task.CompletedTask;
    }

    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        // Never log the OTP itself
        _logger.LogWarning("[MOCK SMS OTP] To: {PhoneNumber} | OTP generated (code not logged)", phoneNumber);
        return Task.CompletedTask;
    }
}