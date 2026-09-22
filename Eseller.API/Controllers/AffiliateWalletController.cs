using Eseller.Application.Features.Affiliate.GetAffiliateWallet;
using Eseller.Application.Features.Affiliate.GetWalletTransactions;
using Eseller.Application.Features.Affiliate.GetWithdrawals;
using Eseller.Application.Features.Affiliate.GetWithdrawalMethods;
using Eseller.Application.Features.Affiliate.RequestWithdrawal;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/affiliate")]
[Authorize(Roles = "Affiliate")]
public class AffiliateWalletController : ControllerBase
{
    private readonly ISender _sender;

    public AffiliateWalletController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get own wallet balance.
    /// </summary>
    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet(CancellationToken cancellationToken)
    {
        var query = new GetAffiliateWalletQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get own wallet transactions.
    /// </summary>
    [HttpGet("wallet/transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? direction,
        [FromQuery] string? transactionType,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetWalletTransactionsQuery(
            direction, transactionType, pageNumber, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Request withdrawal.
    /// </summary>
    [HttpPost("withdrawal/request")]
    public async Task<IActionResult> RequestWithdrawal(
        [FromBody] RequestWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RequestWithdrawalCommand(request.Amount, request.PayoutMethod);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            withdrawalId = result.Value,
            message = "Withdrawal request submitted successfully."
        });
    }

    /// <summary>
    /// Get own withdrawal history.
    /// </summary>
    [HttpGet("withdrawal/history")]
    public async Task<IActionResult> GetWithdrawals(
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetWithdrawalsQuery(status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get available payout methods.
    /// </summary>
    [HttpGet("withdrawal/methods")]
    public async Task<IActionResult> GetMethods(CancellationToken cancellationToken)
    {
        var query = new GetWithdrawalMethodsQuery();
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
            "AFFILIATE_INACTIVE" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "BELOW_MINIMUM" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "INSUFFICIENT_BALANCE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "PENDING_WITHDRAWAL_EXISTS" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record RequestWithdrawalRequest(decimal Amount, string PayoutMethod);