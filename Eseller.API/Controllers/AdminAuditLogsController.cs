using Eseller.Application.Features.AdminAuditLogs.GetAuditLogs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminAuditLogsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? actorType,
        [FromQuery] Guid? actorAccountId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 200) pageSize = 200;
        if (pageSize < 1) pageSize = 50;

        var query = new GetAuditLogsQuery(
            actorType, actorAccountId, action, entityType, entityId,
            fromDate, toDate, pageNumber, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}