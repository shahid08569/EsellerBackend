namespace Eseller.Infrastructure.Settings;

public class FileStorageSettings
{
    public const string SectionName = "FileStorageSettings";

    /// <summary>
    /// Base path where uploaded files are stored locally.
    /// </summary>
    public string BasePath { get; set; } = "wwwroot/uploads";

    /// <summary>
    /// Base URL for serving uploaded files (e.g., https://api.eseller.com).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}