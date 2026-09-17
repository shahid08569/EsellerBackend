using Eseller.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.Services;

public class MockSmsService : ISmsService
{
    private readonly ILogger<MockSmsService> _logger;

    public MockSmsService(ILogger<MockSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[MOCK SMS] To: {PhoneNumber} | Message: {Message}",
            phoneNumber, message);

        return Task.CompletedTask;
    }

    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[MOCK SMS OTP] To: {PhoneNumber} | OTP Code: {Code}",
            phoneNumber, code);

        return Task.CompletedTask;
    }
}