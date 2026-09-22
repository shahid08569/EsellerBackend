using Eseller.Application.Common;
using Eseller.Application.Features.Affiliate.GetAffiliateConversionStats;
using Eseller.Application.Features.Affiliate.GetAffiliateConversions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/affiliate/conversions")]
[Authorize(Roles = "Affiliate")]
public class AffiliateConversionController : ControllerBase
{
    private readonly ISender _sender;

    public AffiliateConversionController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get own conversion history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetConversions(
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetAffiliateConversionsQuery(status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get conversion statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var query = new GetAffiliateConversionStatsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}