namespace Eseller.Application.Abstractions;

public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);

    Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// When true, OTP may be returned in API responses for local/dev testing (mock SMS).
    /// </summary>
    bool IsMock { get; }
}
