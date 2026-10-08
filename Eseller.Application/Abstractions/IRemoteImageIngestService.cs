namespace Eseller.Application.Abstractions;

/// <summary>
/// Downloads an external image URL and stores it under local /uploads for a category.
/// Returns the relative /uploads/... path, or null if the URL is already local / not external.
/// </summary>
public interface IRemoteImageIngestService
{
    Task<string?> IngestCategoryImageAsync(
        Guid categoryId,
        string imageUrl,
        CancellationToken cancellationToken = default);
}
