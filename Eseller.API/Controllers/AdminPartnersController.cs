using Eseller.Application.Features.AdminPartners.AddPartner;
using Eseller.Application.Features.AdminPartners.DeactivatePartner;
using Eseller.Application.Features.AdminPartners.GetPartners;
using Eseller.Application.Features.AdminPartners.UpdatePartner;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/partners")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminPartnersController : ControllerBase
{
    private readonly ISender _sender;

    public AdminPartnersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetPartners(
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetPartnersQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> AddPartner(
        [FromBody] AddPartnerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddPartnerCommand(
            request.Email, request.Password, request.Permissions);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            partnerId = result.Value,
            message = "Partner added successfully."
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdatePartner(
        Guid id,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePartnerCommand(id, request.Permissions);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Partner updated successfully." });
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivatePartnerCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Partner deactivated successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "EMAIL_EXISTS" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
            "PARTNER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "ACCOUNT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "ALREADY_INACTIVE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "EMAIL_EXISTS" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
            "PARTNER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "ACCOUNT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "ALREADY_INACTIVE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record AddPartnerRequest(
    string Email,
    string Password,
    string? Permissions);

public record UpdatePartnerRequest(
    string? Permissions);