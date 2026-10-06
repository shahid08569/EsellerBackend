using Eseller.Application.Features.Dashboard.GetNotifications;
using Eseller.Application.Features.Dashboard.GetUnreadNotificationCount;
using Eseller.Application.Features.Dashboard.MarkAllNotificationsRead;
using Eseller.Application.Features.Dashboard.MarkNotificationRead;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetNotificationsQuery(unreadOnly, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "REQUEST_CANCELLED")
                return NoContent();
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(result.Value);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUnreadNotificationCountQuery(), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "REQUEST_CANCELLED")
                return NoContent();
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(new { count = result.Value });
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkNotificationReadCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOTIFICATION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Notification marked as read." });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "All notifications marked as read." });
    }
}
