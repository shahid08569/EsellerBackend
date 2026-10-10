using Eseller.Application.Abstractions;
using Eseller.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
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
        IConfiguration configuration,
        ILogger<LocalFileStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _rootPath = UploadsPathResolver.Resolve(env, configuration);
        _logger.LogInformation("File storage root: {RootPath}", _rootPath);
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg", ".pdf", ".doc", ".docx"
    };

    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".vbs", ".php", ".phtml",
        ".asp", ".aspx", ".jsp", ".jar", ".cgi", ".pl", ".py", ".config", ".htaccess"
    };

    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (DangerousExtensions.Contains(ext) || !AllowedExtensions.Contains(ext))
        {
            _logger.LogWarning("Rejected file upload with disallowed extension: {FileName}", fileName);
            throw new InvalidOperationException("File extension is not allowed for security reasons.");
        }

        var cleanRel = NormalizeRelativePath(fileName);
        var fullPath = ResolveUnderRoot(cleanRel);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        // Copy stream into memory buffer to validate magic bytes and content
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
            throw new InvalidOperationException("Cannot upload an empty file.");

        // Maximum upload limit: 20 MB
        if (bytes.Length > 20 * 1024 * 1024)
            throw new InvalidOperationException("File size exceeds 20 MB limit.");

        // Validate file content signature (Magic Bytes)
        ValidateFileSignature(ext, bytes);

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        var relativeUrl = "/uploads/" + cleanRel.Replace('\\', '/');
        _logger.LogDebug("File uploaded securely: {RelativeUrl} -> {FullPath}", relativeUrl, fullPath);
        return relativeUrl;
    }

    private static void ValidateFileSignature(string extension, byte[] bytes)
    {
        if (bytes.Length < 4) return;

        switch (extension)
        {
            case ".jpg" or ".jpeg":
                // JPEG magic bytes: FF D8 FF
                if (bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
                    throw new InvalidOperationException("Invalid JPEG file content.");
                break;

            case ".png":
                // PNG magic bytes: 89 50 4E 47
                if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                    throw new InvalidOperationException("Invalid PNG file content.");
                break;

            case ".gif":
                // GIF magic bytes: GIF8
                if (bytes[0] != 0x47 || bytes[1] != 0x49 || bytes[2] != 0x46 || bytes[3] != 0x38)
                    throw new InvalidOperationException("Invalid GIF file content.");
                break;

            case ".pdf":
                // PDF magic bytes: %PDF
                if (bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46)
                    throw new InvalidOperationException("Invalid PDF file content.");
                break;

            case ".svg":
                // SVG safety check: inspect for dangerous scripts
                var text = System.Text.Encoding.UTF8.GetString(bytes).ToLowerInvariant();
                if (text.Contains("<script") || text.Contains("javascript:") || text.Contains("onload=") || text.Contains("onerror="))
                    throw new InvalidOperationException("SVG file contains disallowed executable scripts.");
                break;
        }
    }

    public Task DeleteAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        // Never try to delete absolute external CDN URLs from disk
        if (fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
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
