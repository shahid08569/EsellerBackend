using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Eseller.Infrastructure.Services;

public class VerificationTokenService : IVerificationTokenService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public VerificationTokenService(IApplicationDbContext context, IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<string> CreateTokenAsync(
        Guid accountId,
        TokenPurpose purpose,
        VerificationChannel channel,
        int expiryMinutes,
        CancellationToken cancellationToken = default)
    {
        // Generate 6-digit numeric code
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        // Hash the code
        var codeHash = HashCode(code);

        // Mark any existing unconsumed tokens as consumed
        var existingTokens = await _context.VerificationTokens
            .Where(vt => vt.AccountId == accountId
                      && vt.Purpose == purpose
                      && vt.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var existingToken in existingTokens)
        {
            existingToken.ConsumedAt = _dateTime.UtcNow;
        }

        // Create new token
        _context.VerificationTokens.Add(new VerificationToken
        {
            AccountId = accountId,
            Purpose = purpose,
            CodeHash = codeHash,
            Channel = channel,
            ExpiresAt = _dateTime.UtcNow.AddHours(expiryMinutes),
            Attempts = 0,
            CreatedAt = _dateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return code;   // Return plain code (sirf email/SMS mein jayega)
    }

    public async Task<bool> ValidateTokenAsync(
        Guid accountId,
        TokenPurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
    {
        var codeHash = HashCode(code);

        var token = await _context.VerificationTokens
            .Where(vt => vt.AccountId == accountId
                      && vt.Purpose == purpose
                      && vt.ConsumedAt == null)
            .OrderByDescending(vt => vt.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null) return false;

        // Check expiry
        if (token.ExpiresAt < _dateTime.UtcNow)
        {
            token.ConsumedAt = _dateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }

        // Increment attempts
        token.Attempts++;

        // Check hash match
        if (token.CodeHash != codeHash)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }

        // Success — mark as consumed
        token.ConsumedAt = _dateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> GetRecentAttemptsCountAsync(
        Guid accountId,
        TokenPurpose purpose,
        TimeSpan timeWindow,
        CancellationToken cancellationToken = default)
    {
        var since = _dateTime.UtcNow.Subtract(timeWindow);

        return await _context.VerificationTokens
            .Where(vt => vt.AccountId == accountId
                      && vt.Purpose == purpose
                      && vt.CreatedAt >= since)
            .CountAsync(cancellationToken);
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}