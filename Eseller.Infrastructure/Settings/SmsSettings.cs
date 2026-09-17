namespace Eseller.Infrastructure.Settings;

public class SmsSettings
{
    public const string SectionName = "SmsSettings";

    // Mock mode — dev ke liye (console pe print)
    public bool UseMock { get; set; } = true;

    // Twilio / real provider settings (baad ke liye)
    public string? Provider { get; set; }
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? FromNumber { get; set; }
}