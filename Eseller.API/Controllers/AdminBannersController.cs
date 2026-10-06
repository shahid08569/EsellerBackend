using Eseller.Application.Features.Banners.CreateBanner;
using Eseller.Application.Features.Banners.DeleteBanner;
using Eseller.Application.Features.Banners.GetBannerById;
using Eseller.Application.Features.Banners.GetHomepageBanners;
using Eseller.Application.Features.Banners.UpdateBanner;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/banners")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminBannersController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBannersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetBanners(
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 200) pageSize = 200;
        if (pageSize < 1) pageSize = 50;

        var query = new GetHomepageBannersQuery(isActive, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetBannerByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBannerCommand(
            request.ImageUrl,
            request.LinkUrl,
            request.Title,
            request.Subtitle,
            request.Description,
            request.Price,
            request.OriginalPrice,
            request.ButtonText,
            request.BackgroundColor,
            request.TextColor,
            request.AvailableSizes,
            request.SortOrder,
            request.IsActive,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            bannerId = result.Value,
            message = "Banner created successfully."
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBannerCommand(
            id,
            request.ImageUrl,
            request.LinkUrl,
            request.Title,
            request.Subtitle,
            request.Description,
            request.Price,
            request.OriginalPrice,
            request.ButtonText,
            request.BackgroundColor,
            request.TextColor,
            request.AvailableSizes,
            request.SortOrder,
            request.IsActive,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Banner updated successfully." });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        [FromForm] IFormFile? file,
        [FromServices] Eseller.Application.Abstractions.IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file provided", errorCode = "NO_FILE" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { error = "Invalid file type. Only JPG, PNG, and WEBP images are allowed.", errorCode = "INVALID_FILE_TYPE" });

            await using var stream = file.OpenReadStream();
            var uniqueFileName = $"banners/banner-{Guid.NewGuid()}{ext}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            var savedPath = await fileStorage.UploadAsync(stream, uniqueFileName, contentType, cancellationToken);

            return Ok(new { imageUrl = savedPath });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = $"Banner image upload failed: {ex.Message}",
                errorCode = "UPLOAD_FAILED"
            });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteBannerCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Banner deleted successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "BANNER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "BANNER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record CreateBannerRequest(
    string ImageUrl,
    string? LinkUrl,
    string? Title,
    string? Subtitle,
    string? Description,
    decimal? Price,
    decimal? OriginalPrice,
    string? ButtonText,
    string? BackgroundColor,
    string? TextColor,
    string? AvailableSizes,
    int SortOrder,
    bool IsActive = true,
    DateTime? StartDate = null,
    DateTime? EndDate = null);

public record UpdateBannerRequest(
    string ImageUrl,
    string? LinkUrl,
    string? Title,
    string? Subtitle,
    string? Description,
    decimal? Price,
    decimal? OriginalPrice,
    string? ButtonText,
    string? BackgroundColor,
    string? TextColor,
    string? AvailableSizes,
    int SortOrder,
    bool IsActive,
    DateTime? StartDate,
    DateTime? EndDate);