using Eseller.Application.Features.ProductVariants.CreateProductVariant;
using Eseller.Application.Features.ProductVariants.DeleteProductVariant;
using Eseller.Application.Features.ProductVariants.DeleteVariantImage;
using Eseller.Application.Features.ProductVariants.GetProductVariantById;
using Eseller.Application.Features.ProductVariants.GetProductVariants;
using Eseller.Application.Features.ProductVariants.UpdateProductVariant;
using Eseller.Application.Features.ProductVariants.UploadVariantImage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/products/{productId:guid}/variants")]
public class ProductVariantsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductVariantsController(ISender sender)
    {
        _sender = sender;
    }

    // ─────────────────────────────────────────────────────────────
    // PUBLIC ENDPOINTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Get all variants for a product (public).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var query = new GetProductVariantsQuery(productId);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get a specific variant (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid productId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetProductVariantByIdQuery(productId, id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    // ─────────────────────────────────────────────────────────────
    // SHOPKEEPER / ADMIN ENDPOINTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Create a new variant (shopkeeper own / admin any).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Create(
        Guid productId,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductVariantCommand(
            productId,
            request.Sku,
            request.Price,
            request.StockQty,
            request.LowStockThreshold,
            request.Attributes);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { variantId = result.Value, message = "Variant created successfully." });
    }

    /// <summary>
    /// Update a variant (shopkeeper own / admin any).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Update(
        Guid productId,
        Guid id,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductVariantCommand(
            productId,
            id,
            request.Sku,
            request.Price,
            request.StockQty,
            request.LowStockThreshold,
            request.IsActive,
            request.Attributes);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Variant updated successfully." });
    }

    /// <summary>
    /// Delete a variant (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Delete(
        Guid productId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteProductVariantCommand(productId, id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Variant deleted successfully." });
    }

    /// <summary>
    /// Upload variant image.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        Guid productId,
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadVariantImageCommand
        {
            ProductId = productId,
            VariantId = id,
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
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { imageUrl = result.Value, message = "Image uploaded successfully." });
    }

    /// <summary>
    /// Delete variant image.
    /// </summary>
    [HttpDelete("{id:guid}/image")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> DeleteImage(
        Guid productId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteVariantImageCommand(productId, id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "IMAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),   // ← ADD
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Image deleted successfully." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record CreateProductVariantRequest(
    string Sku,
    decimal Price,
    int StockQty,
    int LowStockThreshold,
    List<CreateVariantAttribute> Attributes);

public record UpdateProductVariantRequest(
    string Sku,
    decimal Price,
    int StockQty,
    int LowStockThreshold,
    bool IsActive,
    List<UpdateVariantAttribute> Attributes);