using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.RegisterSuperAdmin;

/// <summary>
/// Bootstrap-only SuperAdmin signup. Allowed only when zero SuperAdmin accounts exist.
/// </summary>
public class RegisterSuperAdminCommandHandler
    : IRequestHandler<RegisterSuperAdminCommand, Result<RegisterSuperAdminResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILocationService _locationService;
    private readonly IDateTime _dateTime;

    public RegisterSuperAdminCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILocationService locationService,
        IDateTime dateTime)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _locationService = locationService;
        _dateTime = dateTime;
    }

    public async Task<Result<RegisterSuperAdminResponse>> Handle(
        RegisterSuperAdminCommand request,
        CancellationToken cancellationToken)
    {
        var adminExists = await _context.Accounts
            .AnyAsync(a => a.RoleType == RoleType.SuperAdmin, cancellationToken);

        if (adminExists)
        {
            return Result.Failure<RegisterSuperAdminResponse>(
                "SuperAdmin already exists. Signup is closed. Use login instead.",
                "SUPERADMIN_EXISTS");
        }

        if (!await _locationService.IsValidLocationAsync(
                request.Latitude, request.Longitude, cancellationToken))
        {
            return Result.Failure<RegisterSuperAdminResponse>(
                "Valid location is required to register.",
                "LOCATION_REQUIRED");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.Name.Trim();

        var emailTaken = await _context.Accounts
            .AnyAsync(a => a.Email.ToLower() == email || a.Username.ToLower() == email, cancellationToken);

        if (emailTaken)
        {
            return Result.Failure<RegisterSuperAdminResponse>(
                "Email is already registered.",
                "EMAIL_TAKEN");
        }

        var username = email.Contains('@')
            ? email.Split('@')[0]
            : email;
        if (username.Length < 3) username = "superadmin";

        var usernameTaken = await _context.Accounts
            .AnyAsync(a => a.Username.ToLower() == username.ToLower(), cancellationToken);
        if (usernameTaken)
            username = $"admin-{Guid.NewGuid():N}"[..16];

        var account = new Account
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleType = RoleType.SuperAdmin,
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = true
        };
        account.SetCreatedAt(_dateTime.UtcNow);

        _context.Accounts.Add(account);
        _context.SuperAdmins.Add(new SuperAdmin { AccountId = account.Id });

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _locationService.LogLocationAsync(
                account.Id,
                request.Latitude,
                request.Longitude,
                string.IsNullOrWhiteSpace(request.DeviceType) ? "Desktop" : request.DeviceType,
                "RegisterSuperAdmin",
                null,
                null,
                cancellationToken);
        }
        catch
        {
            // Ignore location logging failures
        }

        return Result.Success(new RegisterSuperAdminResponse(
            account.Id,
            account.Email,
            "SuperAdmin account created. You can sign in now."));
    }
}
