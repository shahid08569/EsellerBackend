using Eseller.Application.Features.Orders.BuyNow;
using Eseller.Application.Features.Orders.CreateOrderRequest;
using Eseller.Application.Features.Orders.GetMyOrders;
using Eseller.Application.Features.Orders.GetOrderById;
using Eseller.Application.Features.Orders.GetOrderStatusHistory;
using Eseller.Application.Features.Orders.GetOrderTracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "User")]
public class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create order from cart.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var items = request.Items?.Select(i => new CreateOrderItemInput(i.ProductVariantId, i.Quantity)).ToList();
        var command = new CreateOrderRequestCommand(
            request.ReferralCode,
            request.CustomerName,
            request.CustomerPhone,
            request.ShippingAddress,
            request.City,
            request.State,
            request.Country,
            request.OrderNotes,
            items);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CART_EMPTY" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "INSUFFICIENT_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "SELF_REFERRAL" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { orderId = result.Value, message = "Order request created." });
    }

    /// <summary>
    /// Buy now — direct order from single item.
    /// </summary>
    [HttpPost("buy-now")]
    public async Task<IActionResult> BuyNow(
        [FromBody] BuyNowRequest request,
        CancellationToken cancellationToken)
    {
        var command = new BuyNowCommand(
            request.ProductVariantId,
            request.Quantity,
            request.ReferralCode,
            request.CustomerName,
            request.CustomerPhone,
            request.ShippingAddress,
            request.City,
            request.State,
            request.Country,
            request.OrderNotes);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "VARIANT_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "INSUFFICIENT_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "SELF_REFERRAL" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { orderId = result.Value, message = "Order request created." });
    }

    /// <summary>
    /// Get current user's orders (paginated).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyOrdersQuery(pageNumber, pageSize);
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
    /// Get order by ID (customer's own order only).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(id);
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


    /// <summary>
    /// Get order status history (timeline).
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

    /// <summary>
    /// Get order tracking info with timeline.
    /// </summary>
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> GetTracking(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetOrderTrackingQuery(id);
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

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record CreateOrderItemRequest(Guid ProductVariantId, int Quantity);

public record CreateOrderRequest(
    string? ReferralCode,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ShippingAddress = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? OrderNotes = null,
    List<CreateOrderItemRequest>? Items = null);

public record BuyNowRequest(
    Guid ProductVariantId,
    int Quantity,
    string? ReferralCode = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ShippingAddress = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? OrderNotes = null);