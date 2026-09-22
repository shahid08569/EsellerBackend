using Eseller.Application.Features.AdminAffiliate.ApproveAffiliateCommission;
using Eseller.Application.Features.AdminAffiliate.GetAffiliateCommissions;
using Eseller.Application.Features.AdminAffiliate.RejectAffiliateCommission;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/affiliate/commissions")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminAffiliateCommissionsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminAffiliateCommissionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all affiliate commissions (with filters).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCommissions(
        [FromQuery] string? status,
        [FromQuery] Guid? affiliateId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetAffiliateCommissionsQuery(status, affiliateId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Approve a pending commission (after hold period).
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveAffiliateCommissionCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Commission approved successfully." });
    }

    /// <summary>
    /// Reject a pending commission.
    /// </summary>
    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new RejectAffiliateCommissionCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Commission rejected successfully." });
    }

    // ✅ Generic — Result<T>
    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "COMMISSION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_STATUS" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "ORDER_NOT_DELIVERED" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "RETURN_WINDOW_ACTIVE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    // ✅ Non-generic — Result
    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "COMMISSION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "AFFILIATE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_STATUS" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "ORDER_NOT_DELIVERED" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "RETURN_WINDOW_ACTIVE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}