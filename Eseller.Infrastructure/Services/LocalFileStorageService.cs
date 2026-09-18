using Eseller.Application.Abstractions;
using Eseller.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eseller.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageSettings _settings;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IOptions<FileStorageSettings> settings,
        ILogger<LocalFileStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // 1. Ensure directory exists
        var fullPath = Path.Combine(_settings.BasePath, fileName);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 2. Write file
        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        // 3. Return relative URL (e.g., /uploads/shops/xxx/logo.jpg)
        var relativeUrl = "/" + fileName.Replace("\\", "/");

        _logger.LogInformation("File uploaded: {RelativeUrl}", relativeUrl);

        return relativeUrl;
    }

    public Task DeleteAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        // Convert URL to file path
        var relativePath = fileUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
        var fullPath = Path.Combine(_settings.BasePath, relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {FileUrl}", fileUrl);
        }

        return Task.CompletedTask;
    }
}