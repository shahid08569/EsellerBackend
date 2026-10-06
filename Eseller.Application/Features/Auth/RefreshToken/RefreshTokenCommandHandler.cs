using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using RefreshTokenEntity = Eseller.Domain.Entities.Identity.RefreshToken;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTime _dateTime;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTime dateTime)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTime = dateTime;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Hash the incoming refresh token
        var tokenHash = _jwtService.HashToken(request.RefreshToken);

        // 2. Find the token in database
        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return Result.Failure<RefreshTokenResponse>(
                "Invalid refresh token.",
                "INVALID_REFRESH_TOKEN");
        }

        // 3. Check if token is already revoked (REUSE DETECTION)
        if (existingToken.RevokedAt is not null)
        {
            var familyTokens = await _context.RefreshTokens
                .Where(rt => rt.FamilyId == existingToken.FamilyId
                          && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var familyToken in familyTokens)
            {
                familyToken.Revoke(_dateTime.UtcNow);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Failure<RefreshTokenResponse>(
                "Refresh token has been revoked. All sessions in this family have been terminated.",
                "TOKEN_REUSE_DETECTED");
        }

        // 4. Check expiry
        if (existingToken.ExpiresAt < _dateTime.UtcNow)
        {
            return Result.Failure<RefreshTokenResponse>(
                "Refresh token has expired. Please login again.",
                "REFRESH_TOKEN_EXPIRED");
        }

        // 5. Load the account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == existingToken.AccountId, cancellationToken);

        if (account is null || !account.IsActive)
        {
            return Result.Failure<RefreshTokenResponse>(
                "Account not found or inactive.",
                "ACCOUNT_INACTIVE");
        }

        // 6. Generate new tokens
        var newAccessToken = _jwtService.GenerateAccessToken(
            account.Id,
            account.Username,
            account.RoleType);

        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var newRefreshTokenHash = _jwtService.HashToken(newRefreshToken);

        // 7. Revoke old token
        existingToken.Revoke(_dateTime.UtcNow);

        // 8. Create new refresh token (same family)
        var newRefreshTokenEntity = new RefreshTokenEntity
        {
            AccountId = account.Id,
            TokenHash = newRefreshTokenHash,
            FamilyId = existingToken.FamilyId,
            ExpiresAt = _dateTime.UtcNow.AddDays(7),
            CreatedAt = _dateTime.UtcNow
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);

        // 9. Link old token to new one
        existingToken.SetReplacedBy(newRefreshTokenEntity.Id);

        // 10. Save
        await _context.SaveChangesAsync(cancellationToken);

        // 11. Return new tokens + account info (so frontend can restore full auth state)
        return Result.Success(new RefreshTokenResponse(
            newAccessToken,
            newRefreshToken,
            _dateTime.UtcNow.AddMinutes(_jwtService.AccessTokenExpirationMinutes),
            account.Id,
            account.Username,
            account.Email,
            account.RoleType.ToString()));
    }
}