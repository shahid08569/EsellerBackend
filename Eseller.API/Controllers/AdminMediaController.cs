using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

/// <summary>
/// Shared admin media upload (logos, images) — used when feature-specific upload routes
/// are unavailable or when creating entities that do not have an Id yet.
/// </summary>
[ApiController]
[Route("api/v1/admin/media")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminMediaController : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm(Name = "file")] IFormFile? file,
        [FromQuery] string? folder,
        [FromServices] Eseller.Application.Abstractions.IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        try
        {
            file ??= Request.Form.Files.GetFile("file")
                  ?? Request.Form.Files.GetFile("image")
                  ?? Request.Form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file provided", errorCode = "NO_FILE" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".png";
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new
                {
                    error = "Invalid file type. Only JPG, PNG, WEBP, GIF, and SVG are allowed.",
                    errorCode = "INVALID_FILE_TYPE"
                });

            var safeFolder = string.IsNullOrWhiteSpace(folder)
                ? "logos"
                : new string(folder.Trim().ToLowerInvariant()
                    .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                    .ToArray());
            if (string.IsNullOrWhiteSpace(safeFolder))
                safeFolder = "logos";

            await using var stream = file.OpenReadStream();
            var uniqueFileName = $"{safeFolder}/{safeFolder.TrimEnd('s')}-{Guid.NewGuid()}{ext}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;
            var savedPath = await fileStorage.UploadAsync(stream, uniqueFileName, contentType, cancellationToken);

            return Ok(new { imageUrl = savedPath, logoUrl = savedPath });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = $"Media upload failed: {ex.Message}",
                errorCode = "UPLOAD_FAILED"
            });
        }
    }
}
