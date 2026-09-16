using Eseller.Domain.Enums;

namespace Eseller.Application.Abstractions;

public interface IJwtService
{
    string GenerateAccessToken(Guid accountId, string username, RoleType roleType);
    string GenerateRefreshToken();
    string HashToken(string token);
    Guid? ValidateAccessToken(string token);
}