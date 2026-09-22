using Eseller.Application.Features.Wishlist.AddToWishlist;
using Eseller.Application.Features.Wishlist.ClearWishlist;
using Eseller.Application.Features.Wishlist.GetWishlist;
using Eseller.Application.Features.Wishlist.MoveToCart;
using Eseller.Application.Features.Wishlist.RemoveFromWishlist;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "User")]
public class WishlistController : ControllerBase
{
    private readonly ISender _sender;

    public WishlistController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get current user's wishlist.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWishlist(CancellationToken cancellationToken)
    {
        var query = new GetWishlistQuery();
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
    /// Add product to wishlist.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Add(
        [FromBody] AddToWishlistRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddToWishlistCommand(request.ProductId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "ALREADY_IN_WISHLIST" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { wishlistItemId = result.Value, message = "Product added to wishlist." });
    }

    /// <summary>
    /// Remove product from wishlist.
    /// </summary>
    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveFromWishlistCommand(productId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_IN_WISHLIST" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product removed from wishlist." });
    }

    /// <summary>
    /// Clear entire wishlist.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var command = new ClearWishlistCommand();
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Wishlist cleared." });
    }

    /// <summary>
    /// Move wishlist item to cart.
    /// </summary>
    [HttpPost("{productId:guid}/move-to-cart")]
    public async Task<IActionResult> MoveToCart(
        Guid productId,
        [FromBody] MoveToCartRequest request,
        CancellationToken cancellationToken)
    {
        var command = new MoveToCartCommand(productId, request.ProductVariantId, request.Quantity);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_IN_WISHLIST" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "VARIANT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "OUT_OF_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "INSUFFICIENT_STOCK" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Item moved to cart." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record AddToWishlistRequest(Guid ProductId);
public record MoveToCartRequest(Guid ProductVariantId, int Quantity);