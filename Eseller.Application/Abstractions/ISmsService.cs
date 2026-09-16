namespace Eseller.Application.Abstractions;
public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}