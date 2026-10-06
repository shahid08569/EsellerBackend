using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface IJwtService
{
    string GenerateAccessToken(Guid accountId, string username, RoleType roleType);
    string GenerateRefreshToken();
    string HashToken(string token);
    Guid? ValidateAccessToken(string token);
    /// <summary>Configured access-token lifetime (minutes). Used for client expiry clocks.</summary>
    int AccessTokenExpirationMinutes { get; }
}
