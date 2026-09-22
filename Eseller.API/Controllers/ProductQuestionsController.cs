using Eseller.Application.Features.ProductQuestions.CreateQuestion;
using Eseller.Application.Features.ProductQuestions.DeleteQuestion;
using Eseller.Application.Features.ProductQuestions.GetProductQuestions;
using Eseller.Application.Features.ProductQuestions.UpdateQuestion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1")]
public class ProductQuestionsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductQuestionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("products/{productId:guid}/questions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQuestions(
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductQuestionsQuery(productId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpPost("products/{productId:guid}/questions")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Create(
        Guid productId,
        [FromBody] CreateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateQuestionCommand(productId, request.Question);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_AVAILABLE" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "DUPLICATE_QUESTION" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { questionId = result.Value, message = "Question submitted for approval." });
    }

    [HttpPut("questions/{id:guid}")]
    [Authorize(Roles = "User,SuperAdmin,Partner")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateQuestionCommand(id, request.Question);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "QUESTION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Question updated and pending re-approval." });
    }

    [HttpDelete("questions/{id:guid}")]
    [Authorize(Roles = "User,SuperAdmin,Partner")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteQuestionCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "QUESTION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Question deleted." });
    }
}

public record CreateQuestionRequest(string Question);
public record UpdateQuestionRequest(string Question);