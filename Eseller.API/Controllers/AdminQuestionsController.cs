using Eseller.Application.Features.ProductQuestions.ApproveQuestion;
using Eseller.Application.Features.ProductQuestions.GetPendingQuestions;
using Eseller.Application.Features.ProductQuestions.RejectQuestion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/questions")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminQuestionsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminQuestionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPendingQuestionsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveQuestionCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "QUESTION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Question approved." });
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new RejectQuestionCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "QUESTION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Question rejected." });
    }
}