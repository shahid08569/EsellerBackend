using Eseller.Application.Features.Cms.CreateCmsPage;
using Eseller.Application.Features.Cms.GetCmsPageBySlug;
using Eseller.Application.Features.Cms.UpdateCmsPage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/cms")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminCmsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminCmsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetCmsPageBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCmsPageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCmsPageCommand(request.Slug, request.Title, request.Content);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            pageId = result.Value,
            message = "CMS page created successfully."
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCmsPageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCmsPageCommand(id, request.Title, request.Content);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "CMS page updated successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "PAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "SLUG_EXISTS" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_SLUG" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "PAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "SLUG_EXISTS" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record CreateCmsPageRequest(string Slug, string Title, string Content);
public record UpdateCmsPageRequest(string Title, string Content);