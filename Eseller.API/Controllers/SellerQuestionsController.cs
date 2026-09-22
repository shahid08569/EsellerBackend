using Eseller.Application.Features.ProductQuestions.AnswerQuestion;
using Eseller.Application.Features.ProductQuestions.DeleteAnswer;
using Eseller.Application.Features.ProductQuestions.UpdateAnswer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/seller/questions")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class SellerQuestionsController : ControllerBase
{
    private readonly ISender _sender;

    public SellerQuestionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("{questionId:guid}/answer")]
    public async Task<IActionResult> Answer(
        Guid questionId,
        [FromBody] AnswerQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AnswerQuestionCommand(questionId, request.Answer);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "QUESTION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Answer submitted for approval." });
    }

    [HttpPut("{questionId:guid}/answer")]
    public async Task<IActionResult> UpdateAnswer(
        Guid questionId,
        [FromBody] UpdateAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAnswerCommand(questionId, request.Answer);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ANSWER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Answer updated and pending re-approval." });
    }

    [HttpDelete("{questionId:guid}/answer")]
    public async Task<IActionResult> DeleteAnswer(
        Guid questionId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteAnswerCommand(questionId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ANSWER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Answer deleted." });
    }
}

public record AnswerQuestionRequest(string Answer);
public record UpdateAnswerRequest(string Answer);