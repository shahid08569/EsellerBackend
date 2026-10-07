using Eseller.Application.Features.PaymentShowcase.CreatePaymentLogo;
using Eseller.Application.Features.PaymentShowcase.DeletePaymentLogo;
using Eseller.Application.Features.PaymentShowcase.GetPaymentLogos;
using Eseller.Application.Features.PaymentShowcase.UpdatePaymentLogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/payment-logos")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminPaymentLogosController : ControllerBase
{
    private readonly ISender _sender;

    public AdminPaymentLogosController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPaymentLogosQuery(isActive), cancellationToken);
        if (!result.IsSuccess)
            return HandleError(result);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] UpsertPaymentLogoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreatePaymentLogoCommand(
                request.Name,
                request.ImageUrl,
                request.SortOrder,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { logoId = result.Value, message = "Payment logo created successfully." });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpsertPaymentLogoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdatePaymentLogoCommand(
                id,
                request.Name,
                request.ImageUrl,
                request.SortOrder,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Payment logo updated successfully." });
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        [FromForm(Name = "file")] IFormFile? file,
        [FromServices] Eseller.Application.Abstractions.IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        try
        {
            // Some clients send alternate field names
            file ??= Request.Form.Files.GetFile("file")
                  ?? Request.Form.Files.GetFile("image")
                  ?? Request.Form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file provided", errorCode = "NO_FILE" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".png";
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { error = "Invalid file type. Only JPG, PNG, WEBP, and SVG are allowed.", errorCode = "INVALID_FILE_TYPE" });

            await using var stream = file.OpenReadStream();
            var uniqueFileName = $"payment-logos/logo-{Guid.NewGuid()}{ext}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            var savedPath = await fileStorage.UploadAsync(stream, uniqueFileName, contentType, cancellationToken);

            return Ok(new { imageUrl = savedPath });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = $"Payment logo upload failed: {ex.Message}",
                errorCode = "UPLOAD_FAILED"
            });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeletePaymentLogoCommand(id), cancellationToken);
        if (!result.IsSuccess)
            return HandleError(result);
        return Ok(new { message = "Payment logo deleted successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "PAYMENT_LOGO_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "PAYMENT_LOGO_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record UpsertPaymentLogoRequest(
    string Name,
    string ImageUrl,
    int SortOrder = 0,
    bool IsActive = true);
