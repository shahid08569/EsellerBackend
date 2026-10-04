using Eseller.Application.Features.Reviews.ApproveReview;
using Eseller.Application.Features.Reviews.GetAdminReviews;
using Eseller.Application.Features.Reviews.GetPendingReviews;
using Eseller.Application.Features.Reviews.RejectReview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/reviews")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminReviewsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminReviewsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// List product reviews for moderation. status: pending | approved | all
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReviews(
        [FromQuery] string status = "pending",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminReviewsQuery(status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get all pending reviews.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPendingReviewsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Keep (approve) a review — publishes it on the product.
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveReviewCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "REVIEW_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Review kept and published." });
    }

    /// <summary>
    /// Delete a review permanently.
    /// </summary>
    [HttpPut("{id:guid}/reject")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new RejectReviewCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "REVIEW_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Review deleted." });
    }
}
