using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        => SendAsync(to, subject, body, null, cancellationToken);

    public async Task SendAsync(string to, string subject, string body, string? textBody = null, CancellationToken cancellationToken = default)
    {
        var plain = string.IsNullOrWhiteSpace(textBody)
            ? "Welcome to Eseller Marketplace. Please view this message in an HTML-compatible email client."
            : textBody;

        if (!string.IsNullOrWhiteSpace(_settings.ResendApiKey))
        {
            await SendViaResendAsync(to, subject, body, plain, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || string.IsNullOrWhiteSpace(_settings.SmtpUser))
        {
            _logger.LogWarning(
                "📧 [DEV EMAIL SIMULATOR] SMTP/Resend not configured. Target: {To} | Subject: {Subject}",
                to, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("X-Mailer", "Eseller Platform Mailer");

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body,
                TextBody = plain
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _settings.SmtpHost,
                _settings.SmtpPort,
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrEmpty(_settings.SmtpUser))
            {
                var cleanPassword = _settings.SmtpPassword?.Replace(" ", "").Trim() ?? string.Empty;
                await client.AuthenticateAsync(_settings.SmtpUser, cleanPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully (SMTP) to {To} with subject {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            // Never fail auth/registration because SMTP is down — code is already logged for verification.
            _logger.LogError(ex, "Failed to send email via SMTP to {To}. Continuing without blocking the user flow.", to);
        }
    }

    private async Task SendViaResendAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var from = string.IsNullOrWhiteSpace(_settings.FromEmail)
                ? "Eseller <onboarding@resend.dev>"
                : $"{_settings.FromName} <{_settings.FromEmail}>";

            var client = _httpClientFactory.CreateClient("Resend");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ResendApiKey!.Trim());
            request.Content = JsonContent.Create(new ResendEmailRequest
            {
                From = from,
                To = [to],
                Subject = subject,
                Html = htmlBody,
                Text = textBody
            });

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Resend API failed for {To}. Status={Status}. Body={Body}",
                    to, (int)response.StatusCode, detail);
                return;
            }

            _logger.LogInformation("Email sent successfully (Resend) to {To} with subject {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Resend to {To}. Continuing without blocking the user flow.", to);
        }
    }

    public async Task SendVerificationEmailAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔑 [ACCOUNT VERIFICATION CODE] To: {To} | Code: {Code}", to, code);

        var subject = $"{code} is your Eseller verification code";
        var textBody = $"Welcome to Eseller Marketplace!\n\nYour 6-digit verification code is: {code}\n\nThis code will expire in 24 hours.\nPlease verify to secure your account and track dispatches.\n\nEseller Marketplace Team";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 540px; margin: 0 auto; padding: 28px; border: 1px solid #e5e7eb; border-radius: 16px; background-color: #ffffff;"">
              <div style=""text-align: center; padding-bottom: 20px; border-bottom: 1px solid #f3f4f6;"">
                <h1 style=""color: #E11D48; margin: 0; font-size: 26px; font-weight: 900; letter-spacing: -0.5px;"">Eseller <span style=""font-size: 13px; color: #6b7280; font-weight: 700; text-transform: uppercase;"">Marketplace</span></h1>
              </div>
              <div style=""padding: 24px 0; text-align: center;"">
                <h2 style=""color: #111827; margin-top: 0; font-size: 20px; font-weight: 800;"">Verify Your Email Address</h2>
                <p style=""color: #4b5563; font-size: 14px; line-height: 1.6;"">Welcome to Eseller Marketplace! Use the verification code below to confirm your account:</p>
                <div style=""margin: 24px 0; padding: 16px 28px; background: #fff1f2; border: 2px dashed #E11D48; border-radius: 12px; display: inline-block;"">
                  <span style=""font-size: 34px; font-weight: 900; letter-spacing: 8px; color: #E11D48; font-family: monospace;"">{code}</span>
                </div>
                <p style=""color: #6b7280; font-size: 13px; margin: 8px 0;"">This code will expire in <strong>24 hours</strong>.</p>
                <div style=""margin-top: 24px; padding-top: 16px; border-top: 1px solid #f3f4f6; color: #9ca3af; font-size: 11px; line-height: 1.5;"">
                  If you didn't create an account on Eseller Marketplace, please disregard this email.<br/>
                  &copy; 2026 Eseller Multi-Vendor Platform. 256-Bit SSL Encrypted.
                </div>
              </div>
            </div>";

        await SendAsync(to, subject, body, textBody, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string to, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔑 [PASSWORD RESET CODE] To: {To} | Code: {Code}", to, code);

        var subject = $"{code} is your Eseller password reset code";
        var textBody = $"Password Reset Request - Eseller Marketplace\n\nYour password reset code is: {code}\n\nThis code will expire in 15 minutes. Never share this code with anyone.\n\nEseller Marketplace Team";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 540px; margin: 0 auto; padding: 28px; border: 1px solid #e5e7eb; border-radius: 16px; background-color: #ffffff;"">
              <div style=""text-align: center; padding-bottom: 20px; border-bottom: 1px solid #f3f4f6;"">
                <h1 style=""color: #E11D48; margin: 0; font-size: 26px; font-weight: 900; letter-spacing: -0.5px;"">Eseller <span style=""font-size: 13px; color: #6b7280; font-weight: 700; text-transform: uppercase;"">Marketplace</span></h1>
              </div>
              <div style=""padding: 24px 0; text-align: center;"">
                <h2 style=""color: #111827; margin-top: 0; font-size: 20px; font-weight: 800;"">Password Reset Request</h2>
                <p style=""color: #4b5563; font-size: 14px; line-height: 1.6;"">We received a request to reset your Eseller account password. Use the verification code below to set a new password:</p>
                <div style=""margin: 24px 0; padding: 16px 28px; background: #fff1f2; border: 2px dashed #E11D48; border-radius: 12px; display: inline-block;"">
                  <span style=""font-size: 34px; font-weight: 900; letter-spacing: 8px; color: #E11D48; font-family: monospace;"">{code}</span>
                </div>
                <p style=""color: #6b7280; font-size: 13px; margin: 8px 0;"">This code will expire in <strong>15 minutes</strong>.</p>
                <p style=""color: #ef4444; font-size: 12px; font-weight: bold; margin-top: 16px;"">Never share this code with anyone. Eseller staff will never ask for your code.</p>
                <div style=""margin-top: 24px; padding-top: 16px; border-top: 1px solid #f3f4f6; color: #9ca3af; font-size: 11px; line-height: 1.5;"">
                  If you didn't request a password reset, you can safely ignore this email.<br/>
                  &copy; 2026 Eseller Multi-Vendor Platform.
                </div>
              </div>
            </div>";

        await SendAsync(to, subject, body, textBody, cancellationToken);
    }

    private sealed class ResendEmailRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string[] To { get; set; } = [];

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("html")]
        public string Html { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
