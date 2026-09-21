using Eseller.Application.Features.Orders.UpdateOrderStatus;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public AdminOrdersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Update order status.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrderStatusCommand(id, request.NewStatus);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "INVALID_STATUS_TRANSITION" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "NO_CHANGE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Order status updated." });
    }
}

public record UpdateOrderStatusRequest(OrderStatus NewStatus);