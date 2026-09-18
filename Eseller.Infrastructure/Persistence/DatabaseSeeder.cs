using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly EsellerDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        EsellerDbContext context,
        IPasswordHasher passwordHasher,
        IDateTime dateTime,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedSuperAdminAsync(cancellationToken);
    }

    private async Task SeedSuperAdminAsync(CancellationToken cancellationToken)
    {
        const string superAdminEmail = "superadmin@eseller.com";
        const string superAdminUsername = "superadmin";
        const string superAdminPassword = "SuperAdmin@123"; // ⚠️ Change in production

        // Check if SuperAdmin already exists
        var existingSuperAdmin = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == superAdminEmail, cancellationToken);

        if (existingSuperAdmin is not null)
        {
            _logger.LogInformation("SuperAdmin already exists. Skipping seed.");
            return;
        }

        // Create Account
        var account = new Account
        {
            Username = superAdminUsername,
            Email = superAdminEmail,
            PasswordHash = _passwordHasher.Hash(superAdminPassword),
            RoleType = RoleType.SuperAdmin,
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = true
        };

        _context.Accounts.Add(account);

        // Create SuperAdmin entity
        _context.SuperAdmins.Add(new SuperAdmin
        {
            AccountId = account.Id
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "🔐 SuperAdmin seeded — Email: {Email}, Username: {Username}, Password: {Password}",
            superAdminEmail, superAdminUsername, superAdminPassword);
    }
}