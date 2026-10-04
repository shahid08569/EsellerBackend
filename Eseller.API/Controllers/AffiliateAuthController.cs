using Eseller.Application.Features.Affiliate.GetAffiliateProfile;
using Eseller.Application.Features.Affiliate.RegisterAffiliate;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/affiliate")]
public class AffiliateAuthController : ControllerBase
{
    private readonly ISender _sender;

    public AffiliateAuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Register as affiliate (self signup).
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterAffiliateRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new RegisterAffiliateCommand(
            request.Name,
            request.Email,
            request.Password,
              request.Latitude,
        request.Longitude,
        request.DeviceType,
        ipAddress,        
        userAgent 
            );

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "EMAIL_EXISTS" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            affiliateId = result.Value,
            message = "Affiliate registered successfully. Please login to continue."
        });
    }

    /// <summary>
    /// Get own affiliate profile.
    /// </summary>
    [HttpGet("profile")]
    [Authorize(Roles = "Affiliate")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var query = new GetAffiliateProfileQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "AFFILIATE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTO
// ─────────────────────────────────────────────────────────────

public record RegisterAffiliateRequest(
    string Name,
    string Email,
    string Password,
    decimal Latitude,
    decimal Longitude,
    string DeviceType);