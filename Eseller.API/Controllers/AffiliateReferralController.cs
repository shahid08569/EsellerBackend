using Eseller.Application.Common;
using Eseller.Application.Features.Affiliate.GenerateProductLink;
using Eseller.Application.Features.Affiliate.GetReferralCode;
using Eseller.Application.Features.Affiliate.GetReferralLink;
using Eseller.Application.Features.Affiliate.GetReferralLinkStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/affiliate/referral")]
[Authorize(Roles = "Affiliate")]
public class AffiliateReferralController : ControllerBase
{
    private readonly ISender _sender;

    public AffiliateReferralController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get own referral code with basic stats.
    /// </summary>
    [HttpGet("code")]
    public async Task<IActionResult> GetCode(CancellationToken cancellationToken)
    {
        var query = new GetReferralCodeQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get full referral link.
    /// </summary>
    [HttpGet("link")]
    public async Task<IActionResult> GetLink(CancellationToken cancellationToken)
    {
        var query = new GetReferralLinkQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Generate product-specific referral link.
    /// </summary>
    [HttpPost("link/product/{productId:guid}")]
    public async Task<IActionResult> GenerateProductLink(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var command = new GenerateProductLinkCommand(productId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get referral link statistics (all-time + last 30 days).
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var query = new GetReferralLinkStatsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    //FIX — Full namespace Eseller.Application.Common
    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_INACTIVE" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}