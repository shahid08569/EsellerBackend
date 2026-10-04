using Eseller.Application.Features.Brands.CreateBrand;
using Eseller.Application.Features.Brands.DeleteBrand;
using Eseller.Application.Features.Brands.DeleteBrandLogo;
using Eseller.Application.Features.Brands.UpdateBrand;
using Eseller.Application.Features.Brands.UploadBrandLogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/brands")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminBrandsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBrandsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create a new brand.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBrandCommand(
            request.Name,
            request.LogoUrl,
            request.IsActive ?? true);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { brandId = result.Value, message = "Brand created successfully." });
    }

    /// <summary>
    /// Update a brand.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBrandCommand(id, request.Name, request.IsActive, request.LogoUrl);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "BRAND_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Brand updated successfully." });
    }

    /// <summary>
    /// Soft delete a brand.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteBrandCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "BRAND_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Brand deleted successfully." });
    }

    /// <summary>
    /// Upload brand logo.
    /// </summary>
    [HttpPost("{id:guid}/logo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadBrandLogoCommand
        {
            BrandId = id,
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length
        };

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "BRAND_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { logoUrl = result.Value, message = "Logo uploaded successfully." });
    }

    /// <summary>
    /// Delete brand logo.
    /// </summary>
    [HttpDelete("{id:guid}/logo")]
    public async Task<IActionResult> DeleteLogo(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteBrandLogoCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "BRAND_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Logo deleted successfully." });
    }
}

public record CreateBrandRequest(
    string Name,
    string? LogoUrl = null,
    bool? IsActive = true);

public record UpdateBrandRequest(
    string Name,
    bool IsActive,
    string? LogoUrl = null);