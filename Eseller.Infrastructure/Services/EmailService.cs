using Eseller.Application.Abstractions;
using Eseller.Infrastructure.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Eseller.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = body };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _settings.SmtpHost,
                _settings.SmtpPort,
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrEmpty(_settings.SmtpUser))
            {
                await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent to {To} with subject {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }

    public async Task SendVerificationEmailAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        var subject = "Verify your Eseller account";
        var body = $@"
            <h2>Welcome to Eseller!</h2>
            <p>Please verify your email address using the code below:</p>
            <h1 style='color: #4f46e5;'>{code}</h1>
            <p>This code will expire in 24 hours.</p>
            <p>If you didn't create an account, please ignore this email.</p>";

        await SendAsync(to, subject, body, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        var subject = "Reset your Eseller password";
        var body = $@"
            <h2>Password Reset Request</h2>
            <p>You requested to reset your password. Use the code below:</p>
            <h1 style='color: #4f46e5;'>{code}</h1>
            <p>This code will expire in 1 hour.</p>
            <p>If you didn't request this, please ignore this email.</p>";

        await SendAsync(to, subject, body, cancellationToken);
    }
}