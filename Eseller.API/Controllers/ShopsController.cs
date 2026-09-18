using Eseller.Application.Features.Shops.DeleteShop;
using Eseller.Application.Features.Shops.DeleteShopBanner;
using Eseller.Application.Features.Shops.DeleteShopLogo;
using Eseller.Application.Features.Shops.GetMyShops;
using Eseller.Application.Features.Shops.GetShopById;
using Eseller.Application.Features.Shops.GetShopBySlug;
using Eseller.Application.Features.Shops.GetShops;
using Eseller.Application.Features.Shops.UpdateShop;
using Eseller.Application.Features.Shops.UploadShopBanner;
using Eseller.Application.Features.Shops.UploadShopLogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ShopsController : ControllerBase
{
    private readonly ISender _sender;

    public ShopsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get current shopkeeper's own shops.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> GetMy(
        CancellationToken cancellationToken)
    {
        var query = new GetMyShopsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get shop details by slug (public storefront).
    /// </summary>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetShopBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
   [FromQuery] int pageNumber = 1,
   [FromQuery] int pageSize = 20,
   CancellationToken cancellationToken = default)
    {
        var query = new GetShopsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }


    /// <summary>
    /// Get shop details by ID (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetShopByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

  
    /// <summary>
    /// Update own shop (authenticated shopkeeper).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShopRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateShopCommand(
            id,
            request.Name,
            request.Description,
            request.Phone,
            request.Address,
            request.City,
            request.Country);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Shop updated successfully." });
    }

    /// <summary>
    /// Soft delete own shop (authenticated shopkeeper).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Shop deleted successfully." });
    }

    /// <summary>
    /// Upload shop logo (shopkeeper only).
    /// </summary>
    [HttpPost("{id:guid}/logo")]
    [Authorize(Roles = "Shopkeeper")]
    [RequestSizeLimit(5 * 1024 * 1024)]   // 5 MB
    public async Task<IActionResult> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadShopLogoCommand
        {
            ShopId = id,
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
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { logoUrl = result.Value, message = "Logo uploaded successfully." });
    }

    /// <summary>
    /// Upload shop banner (shopkeeper only).
    /// </summary>
    [HttpPost("{id:guid}/banner")]
    [Authorize(Roles = "Shopkeeper")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadBanner(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadShopBannerCommand
        {
            ShopId = id,
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
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { bannerUrl = result.Value, message = "Banner uploaded successfully." });
    }

    /// <summary>
    /// Delete shop logo (shopkeeper only).
    /// </summary>
    [HttpDelete("{id:guid}/logo")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> DeleteLogo(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopLogoCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Logo deleted successfully." });
    }

    /// <summary>
    /// Delete shop banner (shopkeeper only).
    /// </summary>
    [HttpDelete("{id:guid}/banner")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> DeleteBanner(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopBannerCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Banner deleted successfully." });
    }

}

public record UpdateShopRequest(
    string Name,
    string? Description,
    string? Phone,
    string? Address,
    string? City,
    string? Country);