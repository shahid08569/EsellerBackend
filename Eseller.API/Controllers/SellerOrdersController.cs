using Eseller.Application.Features.Orders.GetSellerOrderById;
using Eseller.Application.Features.Orders.GetSellerOrders;
using Eseller.Application.Features.Orders.UpdateSellerOrderStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class SellerOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public SellerOrdersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Shopkeeper: paginated list of their SellerOrders (real commerce orders only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> GetMySellerOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSellerOrdersQuery(pageNumber, pageSize, status);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOPKEEPER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get seller order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetSellerOrderByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SELLER_ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update seller order status — SuperAdmin/Partner only (sellers are read-only).
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "SuperAdmin,Partner")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateSellerOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSellerOrderStatusCommand(id, request.NewStatus);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SELLER_ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "INVALID_STATUS_TRANSITION" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "NO_CHANGE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Seller order status updated." });
    }
}

public record UpdateSellerOrderStatusRequest(Eseller.Domain.Enums.OrderStatus NewStatus);