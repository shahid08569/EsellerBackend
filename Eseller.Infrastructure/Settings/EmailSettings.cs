namespace Eseller.Infrastructure.Settings;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Eseller";
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// When set, emails are sent via Resend HTTP API (inbox delivery).
    /// Free: 3,000/mo · Pro: $20/mo for 50,000 emails.
    /// </summary>
    public string? ResendApiKey { get; set; }
}