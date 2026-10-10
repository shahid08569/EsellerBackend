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
            || host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0")
            return false;

        // Block port scanning / non-standard ports
        if (uri.Port != 80 && uri.Port != 443 && !uri.IsDefaultPort)
            return false;

        // Resolve DNS and verify the resolved IP is not a private, loopback, or cloud-metadata IP
        try
        {
            var ips = System.Net.Dns.GetHostAddresses(uri.DnsSafeHost);
            foreach (var ip in ips)
            {
                if (System.Net.IPAddress.IsLoopback(ip) || IsPrivateOrReservedIp(ip))
                    return false;
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static bool IsPrivateOrReservedIp(System.Net.IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (Link Local / Cloud Metadata 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 127.0.0.0/8
            if (bytes[0] == 127) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
        }

        return false;
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
