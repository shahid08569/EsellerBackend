using Eseller.Application.Features.Products.ApproveProduct;
using Eseller.Application.Features.Products.GetPendingProducts;
using Eseller.Application.Features.Products.RejectProduct;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminProductsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminProductsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get pending products for approval.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPendingProductsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Approve a product.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveProductCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product approved successfully." });
    }

    /// <summary>
    /// Reject a product with a reason.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectProductCommand(id, request.Reason);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product rejected successfully." });
    }
}

public record RejectProductRequest(string Reason);