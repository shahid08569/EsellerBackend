using Eseller.Application.Features.Blog.GetBlogPostBySlug;
using Eseller.Application.Features.Blog.GetBlogPosts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/blog")]
[AllowAnonymous]
public class BlogController : ControllerBase
{
    private readonly ISender _sender;

    public BlogController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get published blog posts (public).
    /// </summary>
    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 50) pageSize = 50;
        if (pageSize < 1) pageSize = 20;

        var query = new GetBlogPostsQuery(categoryId, true, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get single blog post by slug (public).
    /// </summary>
    [HttpGet("posts/{slug}")]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetBlogPostBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "POST_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}