namespace Eseller.Application.Abstractions;
public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendVerificationEmailAsync(string to, string code, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string to, string code, CancellationToken cancellationToken = default);
}