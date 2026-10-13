using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.AdminAffiliate.ApproveWithdrawal;
using Eseller.Application.Features.AdminAffiliate.GetWithdrawals;
using Eseller.Application.Features.AdminAffiliate.ProcessWithdrawal;
using Eseller.Application.Features.AdminAffiliate.RejectWithdrawal;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/affiliate/withdrawals")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminWithdrawalsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminWithdrawalsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all withdrawals with filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWithdrawals(
        [FromQuery] string? status,
        [FromQuery] Guid? accountId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetWithdrawalsQuery(status, accountId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Approve a pending withdrawal.
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        if (!await PartnerPermissionChecker.HasPermissionAsync(context, currentUserService, "finance:manage", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to manage withdrawals.", errorCode = "FORBIDDEN" });

        var command = new ApproveWithdrawalCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Withdrawal approved successfully." });
    }

    /// <summary>
    /// Reject a pending/approved withdrawal.
    /// </summary>
    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        if (!await PartnerPermissionChecker.HasPermissionAsync(context, currentUserService, "finance:manage", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to manage withdrawals.", errorCode = "FORBIDDEN" });

        var command = new RejectWithdrawalCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Withdrawal rejected successfully." });
    }

    /// <summary>
    /// Process (mark as paid) an approved withdrawal.
    /// Debits wallet.
    /// </summary>
    [HttpPut("{id:guid}/process")]
    public async Task<IActionResult> Process(
        Guid id,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        if (!await PartnerPermissionChecker.HasPermissionAsync(context, currentUserService, "finance:manage", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to process withdrawals.", errorCode = "FORBIDDEN" });

        var command = new ProcessWithdrawalCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Withdrawal processed successfully." });
    }

    // ✅ Generic — Result<T>
    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "WITHDRAWAL_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "WALLET_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_STATUS" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "INSUFFICIENT_BALANCE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
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
            "WITHDRAWAL_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "WALLET_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_STATUS" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "INSUFFICIENT_BALANCE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}