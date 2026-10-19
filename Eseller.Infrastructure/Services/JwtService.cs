using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Eseller.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Eseller.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public int AccessTokenExpirationMinutes => 1440;

    public int GetAccessTokenExpirationMinutes(RoleType roleType) => roleType switch
    {
        RoleType.SuperAdmin => 360, // 6 hours
        RoleType.Partner => 360,    // 6 hours
        RoleType.Shopkeeper => 720, // 12 hours
        _ => 1440                  // 24 hours (Customer / Affiliate / default)
    };

    public int GetRefreshTokenExpirationMinutes(RoleType roleType) => roleType switch
    {
        RoleType.SuperAdmin => 360, // 6 hours
        RoleType.Partner => 360,    // 6 hours
        RoleType.Shopkeeper => 720, // 12 hours
        _ => 43200                 // 30 days
    };

    public string GenerateAccessToken(Guid accountId, string username, RoleType roleType, DateTime? expiresAt = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Role, roleType.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiry = expiresAt ?? DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes(roleType));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public Guid? ValidateAccessToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var accountIdClaim = jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
            return Guid.Parse(accountIdClaim);
        }
        catch
        {
            return null;
        }
    }
}