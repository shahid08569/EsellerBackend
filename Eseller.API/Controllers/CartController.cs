using Eseller.Application.Features.Cart.AddToCart;
using Eseller.Application.Features.Cart.ClearCart;
using Eseller.Application.Features.Cart.GetCart;
using Eseller.Application.Features.Cart.RemoveCartItem;
using Eseller.Application.Features.Cart.UpdateCartItem;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "User")]
public class CartController : ControllerBase
{
    private readonly ISender _sender;

    public CartController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get current user's cart with items and totals.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var query = new GetCartQuery();
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
    /// Add item to cart.
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> AddItem(
        [FromBody] AddToCartRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddToCartCommand(request.ProductVariantId, request.Quantity);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "VARIANT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "OUT_OF_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "INSUFFICIENT_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { cartItemId = result.Value, message = "Item added to cart." });
    }

    /// <summary>
    /// Update cart item quantity.
    /// </summary>
    [HttpPut("items/{variantId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid variantId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCartItemCommand(variantId, request.Quantity);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CART_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "CART_ITEM_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "INSUFFICIENT_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Cart item updated." });
    }

    /// <summary>
    /// Remove item from cart.
    /// </summary>
    [HttpDelete("items/{variantId:guid}")]
    public async Task<IActionResult> RemoveItem(
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveCartItemCommand(variantId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CART_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "CART_ITEM_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Item removed from cart." });
    }

    /// <summary>
    /// Clear all items from cart.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        var command = new ClearCartCommand();
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Cart cleared." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record AddToCartRequest(Guid ProductVariantId, int Quantity);
public record UpdateCartItemRequest(int Quantity);