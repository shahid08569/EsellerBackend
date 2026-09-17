using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface IVerificationTokenService
{
    Task<string> CreateTokenAsync(
        Guid accountId,
        TokenPurpose purpose,
        VerificationChannel channel,
        int expiryMinutes,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateTokenAsync(
        Guid accountId,
        TokenPurpose purpose,
        string code,
        CancellationToken cancellationToken = default);

    Task<int> GetRecentAttemptsCountAsync(
        Guid accountId,
        TokenPurpose purpose,
        TimeSpan timeWindow,
        CancellationToken cancellationToken = default);
}