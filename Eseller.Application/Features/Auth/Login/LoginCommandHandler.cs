using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IDateTime _dateTime;
    private readonly ILocationService _locationService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IDateTime dateTime,
        ILocationService locationService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _dateTime = dateTime;
        _locationService = locationService;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate location
        if (!await _locationService.IsValidLocationAsync(
                request.Latitude,
                request.Longitude,
                cancellationToken))
        {
            return Result.Failure<LoginResponse>(
                "Valid location is required to login.",
                "LOCATION_REQUIRED");
        }

        var loginKey = (request.UsernameOrEmail ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(loginKey))
        {
            return Result.Failure<LoginResponse>(
                "Invalid credentials.",
                "INVALID_CREDENTIALS");
        }

        var loginKeyLower = loginKey.ToLowerInvariant();

        // 2. Find account by username or email (trim + case-insensitive)
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a =>
                    a.Username.ToLower() == loginKeyLower
                    || a.Email.ToLower() == loginKeyLower,
                cancellationToken);

        if (account is null)
        {
            return Result.Failure<LoginResponse>(
                "Invalid credentials.",
                "INVALID_CREDENTIALS");
        }

        // 3. Verify password (do not trim — must match registration exactly)
        if (!_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            return Result.Failure<LoginResponse>(
                "Invalid credentials.",
                "INVALID_CREDENTIALS");
        }

        // 4. Check if account is active
        if (!account.IsActive)
        {
            return Result.Failure<LoginResponse>(
                "Account is deactivated. Please contact support.",
                "ACCOUNT_INACTIVE");
        }

        // 5. Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(
            account.Id,
            account.Username,
            account.RoleType);

        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenHash = _jwtService.HashToken(refreshToken);

        // 6. Save refresh token (hashed)
        var refreshTokenEntity = new Eseller.Domain.Entities.Identity.RefreshToken
        {
            AccountId = account.Id,
            TokenHash = refreshTokenHash,
            FamilyId = Guid.NewGuid(),
            ExpiresAt = _dateTime.UtcNow.AddDays(30),
            CreatedAt = _dateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshTokenEntity);

        // 7. Update last login
        account.LastLoginAt = _dateTime.UtcNow;
        account.SetUpdatedAt(_dateTime.UtcNow);

        // 8. Save changes
        await _context.SaveChangesAsync(cancellationToken);

        // 9. Log location
        await _locationService.LogLocationAsync(
            account.Id,
            request.Latitude,
            request.Longitude,
            request.DeviceType,
            "Login",
            null,
            null,
            cancellationToken);

        // 10. Load partner permissions & referral code if applicable
        string? permissions = null;
        string? referralCode = null;
        if (account.RoleType == Eseller.Domain.Enums.RoleType.Partner)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == account.Id, cancellationToken);
            permissions = partner?.Permissions;
            referralCode = partner?.ReferralCode;
        }

        // 11. Return response — client expiry must match JWT lifetime
        return Result.Success(new LoginResponse(
            account.Id,
            account.Username,
            account.Email,
            account.RoleType.ToString(),
            accessToken,
            refreshToken,
            _dateTime.UtcNow.AddMinutes(_jwtService.AccessTokenExpirationMinutes),
            permissions,
            referralCode));
    }
}
