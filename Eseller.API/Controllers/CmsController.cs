using Eseller.Application.Features.Cms.GetCmsPageBySlug;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/cms")]
[AllowAnonymous]
public class CmsController : ControllerBase
{
    private readonly ISender _sender;

    public CmsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get CMS page by slug (public).
    /// </summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetCmsPageBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}