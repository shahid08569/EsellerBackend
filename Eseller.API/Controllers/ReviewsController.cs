using Eseller.Application.Features.Reviews.CreateReview;
using Eseller.Application.Features.Reviews.DeleteReview;
using Eseller.Application.Features.Reviews.GetProductReviews;
using Eseller.Application.Features.Reviews.UpdateReview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1")]
public class ReviewsController : ControllerBase
{
    private readonly ISender _sender;

    public ReviewsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get product reviews (public — approved only).
    /// </summary>
    [HttpGet("products/{productId:guid}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductReviews(
        Guid productId,
        [FromQuery] int? rating,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductReviewsQuery(productId, rating, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Create a review (customer only, verified purchase).
    /// </summary>
    [HttpPost("products/{productId:guid}/reviews")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Create(
        Guid productId,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateReviewCommand(
            productId,
            request.OrderRequestItemId,
            request.Rating,
            request.Title,
            request.Comment);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "ORDER_ITEM_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "NOT_YOUR_ORDER" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                "ORDER_NOT_DELIVERED" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "ALREADY_REVIEWED" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { reviewId = result.Value, message = "Review published successfully." });
    }

    /// <summary>
    /// Update own review.
    /// </summary>
    [HttpPut("reviews/{id:guid}")]
    [Authorize(Roles = "User,SuperAdmin,Partner")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateReviewCommand(id, request.Rating, request.Title, request.Comment);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "REVIEW_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Review updated and pending re-approval." });
    }

    /// <summary>
    /// Delete own review (or admin any).
    /// </summary>
    [HttpDelete("reviews/{id:guid}")]
    [Authorize(Roles = "User,SuperAdmin,Partner")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteReviewCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "REVIEW_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Review deleted." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record CreateReviewRequest(
    Guid? OrderRequestItemId,
    int Rating,
    string? Title,
    string? Comment);

public record UpdateReviewRequest(
    int Rating,
    string? Title,
    string? Comment);