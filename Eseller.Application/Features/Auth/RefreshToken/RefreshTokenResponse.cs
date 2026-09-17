namespace Eseller.Application.Features.Auth.RefreshToken;

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt);