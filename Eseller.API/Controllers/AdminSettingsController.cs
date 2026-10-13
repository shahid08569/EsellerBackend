using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.AdminSettings.GetSettingByKey;
using Eseller.Application.Features.AdminSettings.GetSettings;
using Eseller.Application.Features.AdminSettings.UpdateSetting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminSettingsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminSettingsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all settings (with filters).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings(
        [FromQuery] string? keySearch,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromServices] IApplicationDbContext context = null!,
        [FromServices] ICurrentUserService currentUserService = null!,
        CancellationToken cancellationToken = default)
    {
        if (!await PartnerPermissionChecker.HasPermissionAsync(context, currentUserService, "settings:manage", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to view system settings.", errorCode = "FORBIDDEN" });
        if (pageSize > 200) pageSize = 200;
        if (pageSize < 1) pageSize = 50;

        var query = new GetSettingsQuery(keySearch, isActive, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Get a single setting by key.
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetByKey(
        string key,
        CancellationToken cancellationToken)
    {
        var query = new GetSettingByKeyQuery(key);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Update a setting (SuperAdmin only).
    /// </summary>
    [HttpPut("{key}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(
        string key,
        [FromBody] UpdateSettingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSettingCommand(key, request.Value, request.IsActive);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Setting updated successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "SETTING_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_KEY" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_VALUE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "SETTING_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_KEY" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_VALUE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTO
// ─────────────────────────────────────────────────────────────

public record UpdateSettingRequest(
    string Value,
    bool? IsActive);