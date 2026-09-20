using Eseller.Application.Features.Inventory.AdjustStock;
using Eseller.Application.Features.Inventory.GetLowStockVariants;
using Eseller.Application.Features.Inventory.GetVariantInventory;
using Eseller.Application.Features.Inventory.GetVariantTransactions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class InventoryController : ControllerBase
{
    private readonly ISender _sender;

    public InventoryController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get current inventory for a variant.
    /// </summary>
    [HttpGet("products/{productId:guid}/variants/{variantId:guid}/inventory")]
    public async Task<IActionResult> GetInventory(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var query = new GetVariantInventoryQuery(productId, variantId);
        var result = await _sender.Send(query, cancellationToken);

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

        return Ok(result.Value);
    }

    /// <summary>
    /// Get inventory transaction history (paginated).
    /// </summary>
    [HttpGet("products/{productId:guid}/variants/{variantId:guid}/inventory/transactions")]
    public async Task<IActionResult> GetTransactions(
        Guid productId,
        Guid variantId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVariantTransactionsQuery(productId, variantId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

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

        return Ok(result.Value);
    }

    /// <summary>
    /// Adjust variant stock (manual).
    /// </summary>
    [HttpPost("products/{productId:guid}/variants/{variantId:guid}/inventory/adjust")]
    public async Task<IActionResult> AdjustStock(
        Guid productId,
        Guid variantId,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AdjustStockCommand(productId, variantId, request.NewQuantity, request.Reason);
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

        return Ok(new { message = "Stock adjusted successfully." });
    }

    /// <summary>
    /// Get low stock variants for a shop.
    /// </summary>
    [HttpGet("shops/{shopId:guid}/variants/low-stock")]
    public async Task<IActionResult> GetLowStock(
        Guid shopId,
        CancellationToken cancellationToken)
    {
        var query = new GetLowStockVariantsQuery(shopId);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}

public record AdjustStockRequest(int NewQuantity, string Reason);