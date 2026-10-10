using System.Security.Cryptography;
using System.Text;
using Eseller.Application.Abstractions;

namespace Eseller.Infrastructure.Services;

/// <summary>
/// Timing-attack-resistant HMAC-SHA256 signature validator.
/// Uses CryptographicOperations.FixedTimeEquals to prevent side-channel leaks.
/// </summary>
public class HmacWebhookSignatureValidator : IWebhookSignatureValidator
{
    public bool VerifySignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(keyBytes);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

            // Strip prefix if header is e.g. "sha256=abc..."
            var incomingHex = signatureHeader.Trim();
            if (incomingHex.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                incomingHex = incomingHex["sha256=".Length..];

            var incomingBytes = Convert.FromHexString(incomingHex);

            // Constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(computedHash, incomingBytes);
        }
        catch
        {
            return false;
        }
    }
}
