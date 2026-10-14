namespace Eseller.Application.Features.Auth.RefreshToken;

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    Guid AccountId,
    string Username,
    string Email,
    string RoleType,
    string? Permissions = null);