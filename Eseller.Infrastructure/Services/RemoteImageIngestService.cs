using Eseller.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.Services;

public class RemoteImageIngestService : IRemoteImageIngestService
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<RemoteImageIngestService> _logger;

    public RemoteImageIngestService(
        IHttpClientFactory httpClientFactory,
        IFileStorageService fileStorage,
        ILogger<RemoteImageIngestService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<string?> IngestCategoryImageAsync(
        Guid categoryId,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        var url = (imageUrl ?? string.Empty).Trim();
        if (!IsExternalHttpUrl(url))
            return null;

        var client = _httpClientFactory.CreateClient("CategoryImageMirror");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} fetching image");

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && contentType is not "application/octet-stream")
            throw new InvalidOperationException($"Not an image ({contentType})");

        var ext = GuessExtension(url, contentType);
        if (!AllowedExt.Contains(ext))
            ext = ".jpg";

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        if (ms.Length == 0)
            throw new InvalidOperationException("Empty image body");
        if (ms.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Image exceeds 5 MB");

        ms.Position = 0;
        var fileName = $"categories/{categoryId}/image-{Guid.NewGuid():N}{ext}";
        var stored = await _fileStorage.UploadAsync(ms, fileName, contentType, cancellationToken);
        _logger.LogInformation("Mirrored category {CategoryId} image -> {Stored}", categoryId, stored);
        return stored;
    }

    private static bool IsExternalHttpUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("esellerglobal.com", StringComparison.Ordinal)
            || host is "localhost" or "127.0.0.1")
            return false;

        return true;
    }

    private static string GuessExtension(string url, string contentType)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext) && AllowedExt.Contains(ext))
                return ext.ToLowerInvariant();
        }
        catch { /* ignore */ }

        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
    }
}
