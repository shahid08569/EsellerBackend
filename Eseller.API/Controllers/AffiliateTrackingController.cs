using Eseller.Application.Common;
using Eseller.Application.Features.Affiliate.GetAffiliateClickStats;
using Eseller.Application.Features.Affiliate.GetAffiliateClicks;
using Eseller.Application.Features.Affiliate.TrackClick;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/affiliate")]
public class AffiliateTrackingController : ControllerBase
{
    private readonly ISender _sender;

    public AffiliateTrackingController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Public click tracking endpoint (base link).
    /// </summary>
    [HttpGet("track/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackClick(
        string code,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var command = new TrackClickCommand(code, null, ipAddress, userAgent);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            clickId = result.Value,
            message = "Click tracked successfully."
        });
    }

    /// <summary>
    /// Public click tracking endpoint (product link).
    /// </summary>
    [HttpGet("track/{code}/product/{productId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackProductClick(
        string code,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var command = new TrackClickCommand(code, productId, ipAddress, userAgent);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            clickId = result.Value,
            message = "Product click tracked successfully."
        });
    }

    /// <summary>
    /// Get own click history (affiliate only).
    /// </summary>
    [HttpGet("clicks")]
    [Authorize(Roles = "Affiliate")]
    public async Task<IActionResult> GetClicks(
        [FromQuery] bool? convertedOnly,
        [FromQuery] Guid? productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetAffiliateClicksQuery(
            convertedOnly, productId, pageNumber, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get own click statistics (affiliate only).
    /// </summary>
    [HttpGet("clicks/stats")]
    [Authorize(Roles = "Affiliate")]
    public async Task<IActionResult> GetClickStats(CancellationToken cancellationToken)
    {
        var query = new GetAffiliateClickStatsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    // Full namespace — no ambiguity
    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_INACTIVE" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_CODE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}