using Eseller.Application.Features.Compare.AddToCompare;
using Eseller.Application.Features.Compare.ClearCompare;
using Eseller.Application.Features.Compare.GetCompareDetails;
using Eseller.Application.Features.Compare.GetCompareList;
using Eseller.Application.Features.Compare.RemoveFromCompare;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "User")]
public class CompareController : ControllerBase
{
    private readonly ISender _sender;

    public CompareController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get compare list.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        var query = new GetCompareListQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get side-by-side comparison details.
    /// </summary>
    [HttpGet("details")]
    public async Task<IActionResult> GetDetails(CancellationToken cancellationToken)
    {
        var query = new GetCompareDetailsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Add product to compare.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Add(
        [FromBody] AddToCompareRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddToCompareCommand(request.ProductId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "ALREADY_IN_COMPARE" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                "COMPARE_LIMIT_EXCEEDED" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { compareItemId = result.Value, message = "Product added to compare." });
    }

    /// <summary>
    /// Remove product from compare.
    /// </summary>
    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveFromCompareCommand(productId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_IN_COMPARE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product removed from compare." });
    }

    /// <summary>
    /// Clear entire compare list.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var command = new ClearCompareCommand();
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Compare list cleared." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record AddToCompareRequest(Guid ProductId);