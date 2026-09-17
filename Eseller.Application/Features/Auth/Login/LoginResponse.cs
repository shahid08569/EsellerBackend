namespace Eseller.Application.Features.Auth.Login;

public record LoginResponse(
    Guid AccountId,
    string Username,
    string Email,
    string RoleType,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt);