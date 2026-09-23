using Eseller.Application.Features.AdminDashboard.GetPlatformDashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminDashboardController : ControllerBase
{
    private readonly ISender _sender;

    public AdminDashboardController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get platform dashboard stats.
    /// </summary>
    [HttpGet("platform")]
    public async Task<IActionResult> GetPlatformDashboard(
        CancellationToken cancellationToken)
    {
        var query = new GetPlatformDashboardQuery();
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