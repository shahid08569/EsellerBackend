namespace Eseller.Infrastructure.Settings;

public class SeedSettings
{
    public const string SectionName = "SeedSettings";

    /// <summary>When true, API runs EF MigrateAsync on startup.</summary>
    public bool AutoMigrate { get; set; }

    /// <summary>When true, runs DatabaseSeeder on startup.</summary>
    public bool SeedOnStartup { get; set; } = true;

    public string SuperAdminEmail { get; set; } = "superadmin@eseller.com";
    public string SuperAdminUsername { get; set; } = "superadmin";

    /// <summary>
    /// Only used when creating the FIRST SuperAdmin. Never resets existing accounts.
    /// Prefer user-secrets / env: SeedSettings__SuperAdminPassword
    /// </summary>
    public string SuperAdminPassword { get; set; } = string.Empty;
}
