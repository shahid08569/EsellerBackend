namespace Eseller.Application.Abstractions;

/// <summary>
/// Provides timing-safe HMAC signature verification for external webhooks (Stripe, PayPal, PayFast, etc.)
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Validates an incoming webhook payload signature using HMAC-SHA256 with constant-time comparison.
    /// </summary>
    bool VerifySignature(string payload, string signatureHeader, string secret);
}
