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
        var cleanRel = fileName.TrimStart('/', '\\').Replace('\\', '/');
        if (cleanRel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            cleanRel = cleanRel["uploads/".Length..];

        var fullPath = Path.Combine(_rootPath, cleanRel.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var relativeUrl = "/uploads/" + cleanRel;
        _logger.LogDebug("File uploaded: {RelativeUrl} -> {FullPath}", relativeUrl, fullPath);
        return relativeUrl;
    }

    public Task DeleteAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        var cleanPath = fileUrl.TrimStart('/').Replace('\\', '/');
        if (cleanPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            cleanPath = cleanPath["uploads/".Length..];

        var fullPath = Path.Combine(_rootPath, cleanPath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {FileUrl}", fileUrl);
        }

        return Task.CompletedTask;
    }
}
