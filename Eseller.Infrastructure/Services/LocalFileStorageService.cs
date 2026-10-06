using Eseller.Application.Abstractions;
using Eseller.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eseller.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageSettings _settings;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _rootPath;

    public LocalFileStorageService(
        IOptions<FileStorageSettings> settings,
        IHostEnvironment env,
        ILogger<LocalFileStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var configured = string.IsNullOrWhiteSpace(_settings.BasePath)
            ? "wwwroot/uploads"
            : _settings.BasePath.Trim();

        // Always resolve against API content root so uploads land in Eseller.API/wwwroot/uploads
        // (relative paths previously depended on process CWD and files vanished).
        _rootPath = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));

        Directory.CreateDirectory(_rootPath);
        _logger.LogDebug("File storage root: {RootPath}", _rootPath);
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var cleanRel = NormalizeRelativePath(fileName);
        var fullPath = ResolveUnderRoot(cleanRel);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var relativeUrl = "/uploads/" + cleanRel.Replace('\\', '/');
        _logger.LogDebug("File uploaded: {RelativeUrl} -> {FullPath}", relativeUrl, fullPath);
        return relativeUrl;
    }

    public Task DeleteAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        try
        {
            var cleanPath = NormalizeRelativePath(fileUrl);
            var fullPath = ResolveUnderRoot(cleanPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("File deleted: {FileUrl}", fileUrl);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Rejected unsafe file path for delete: {FileUrl}", fileUrl);
        }

        return Task.CompletedTask;
    }

    private static string NormalizeRelativePath(string fileNameOrUrl)
    {
        var cleanRel = fileNameOrUrl.Trim().TrimStart('/', '\\').Replace('\\', '/');
        if (cleanRel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            cleanRel = cleanRel["uploads/".Length..];

        // Reject path traversal segments
        var segments = cleanRel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException("Invalid file path.");

        return string.Join('/', segments);
    }

    private string ResolveUnderRoot(string relativePath)
    {
        var candidate = Path.GetFullPath(
            Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var rootFull = Path.GetFullPath(_rootPath);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
            rootFull += Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File path escapes storage root.");

        return candidate;
    }
}
