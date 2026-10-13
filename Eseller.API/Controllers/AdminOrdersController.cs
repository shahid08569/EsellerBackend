using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Orders.AdminPlaceOrder;
using Eseller.Application.Features.Orders.DeleteOrder;
using Eseller.Application.Features.Orders.GetAdminOrders;
using Eseller.Application.Features.Orders.GetOrderStatusHistory;
using Eseller.Application.Features.Orders.UpdateOrderPaymentStatus;
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
    /// Get all platform orders (paginated, with search and status filtering).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] Guid? shopId = null,
        [FromQuery] string? search = null,
        [FromServices] IApplicationDbContext context = null!,
        [FromServices] ICurrentUserService currentUserService = null!,
        CancellationToken cancellationToken = default)
    {
        if (!await PartnerPermissionChecker.HasPermissionAsync(context, currentUserService, "orders:view", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to view orders.", errorCode = "FORBIDDEN" });
        if (pageSize > 200) pageSize = 200;
        if (pageSize < 1) pageSize = 20;
        if (pageNumber < 1) pageNumber = 1;

        var query = new GetAdminOrdersQuery(pageNumber, pageSize, status, shopId, search);
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

    /// <summary>
    /// Super Admin places an order to any seller (or live warehouse) product variant.
    /// Appears in the normal platform order list for that shop.
    /// </summary>
    [HttpPost("place")]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] AdminPlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AdminPlaceOrderCommand(
                request.ProductVariantId,
                request.Quantity,
                request.CustomerName,
                request.CustomerPhone,
                request.ShippingAddress,
                request.City,
                request.State,
                request.Country,
                request.OrderNotes,
                request.Items),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_LIVE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "INSUFFICIENT_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { orderId = result.Value, message = "Order placed successfully." });
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

    /// <summary>
    /// Delete an order (SuperAdmin/Partner).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteOrderCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Order deleted successfully." });
    }

    /// <summary>
    /// Super Admin sets payment status (Pending / Paid / Failed / Refunded).
    /// Sellers can only view this — they cannot change it.
    /// </summary>
    [HttpPut("{id:guid}/payment-status")]
    public async Task<IActionResult> UpdatePaymentStatus(
        Guid id,
        [FromBody] UpdateOrderPaymentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateOrderPaymentStatusCommand(id, request.NewStatus),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                "NO_CHANGE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Payment status updated." });
    }

    /// <summary>
    /// Get order status history timeline.
    /// </summary>
    [HttpGet("{id:guid}/status-history")]
    public async Task<IActionResult> GetStatusHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetOrderStatusHistoryQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}

public record UpdateOrderStatusRequest(OrderStatus NewStatus);

public record UpdateOrderPaymentStatusRequest(PaymentStatus NewStatus);

public record AdminPlaceOrderRequest(
    Guid? ProductVariantId = null,
    int? Quantity = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ShippingAddress = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? OrderNotes = null,
    List<AdminPlaceOrderItemDto>? Items = null);