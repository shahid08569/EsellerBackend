using Eseller.Application.Features.Admin.Shopkeepers.ApproveShopkeeper;
using Eseller.Application.Features.Admin.Shopkeepers.GetShopkeepers;
using Eseller.Application.Features.Admin.Shopkeepers.RejectShopkeeper;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all shopkeepers, optionally filtered by status.
    /// </summary>
    [HttpGet("shopkeepers")]
    public async Task<IActionResult> GetShopkeepers(
        [FromQuery] ShopkeeperApprovalStatus? status,
        CancellationToken cancellationToken)
    {
        var query = new GetShopkeepersQuery(status);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Approve a pending shopkeeper application.
    /// </summary>
    [HttpPost("shopkeepers/{id:guid}/approve")]
    public async Task<IActionResult> ApproveShopkeeper(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveShopkeeperCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Shopkeeper approved successfully." });
    }

    /// <summary>
    /// Reject a pending shopkeeper application with a reason.
    /// </summary>
    [HttpPost("shopkeepers/{id:guid}/reject")]
    public async Task<IActionResult> RejectShopkeeper(
        Guid id,
        [FromBody] RejectShopkeeperRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectShopkeeperCommand(id, request.Reason);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Shopkeeper rejected successfully." });
    }
}

public record RejectShopkeeperRequest(string Reason);