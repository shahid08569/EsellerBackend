using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Eseller.Infrastructure.Services;

/// <summary>
/// Resolves the on-disk uploads folder used by LocalFileStorageService and static file middleware.
/// </summary>
public static class UploadsPathResolver
{
    public static string Resolve(IHostEnvironment env, IConfiguration configuration)
    {
        var fromEnv = Environment.GetEnvironmentVariable("ESELLER_UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            var absolute = Path.GetFullPath(fromEnv.Trim());
            Directory.CreateDirectory(absolute);
            return absolute;
        }

        var configured = configuration["FileStorageSettings:BasePath"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = "wwwroot/uploads";

        configured = configured.Trim();
        if (Path.IsPathRooted(configured))
        {
            Directory.CreateDirectory(configured);
            return Path.GetFullPath(configured);
        }

        var primary = Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
        var altSibling = Path.GetFullPath(Path.Combine(env.ContentRootPath, "uploads"));

        // Prefer the folder that already has files (MonsterASP FTP sometimes lands on /wwwroot/uploads).
        if (HasFiles(primary))
            return primary;
        if (HasFiles(altSibling))
            return altSibling;

        Directory.CreateDirectory(primary);
        return primary;
    }

    private static bool HasFiles(string path)
    {
        try
        {
            return Directory.Exists(path)
                && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();
        }
        catch
        {
            return false;
        }
    }
}
