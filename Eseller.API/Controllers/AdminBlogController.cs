using Eseller.Application.Features.Blog.CreateBlogCategory;
using Eseller.Application.Features.Blog.CreateBlogPost;
using Eseller.Application.Features.Blog.DeleteBlogPost;
using Eseller.Application.Features.Blog.GetBlogPosts;
using Eseller.Application.Features.Blog.UpdateBlogPost;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/blog")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminBlogController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBlogController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetBlogPostsQuery(categoryId, null, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost(
        [FromBody] CreateBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBlogPostCommand(
            request.BlogCategoryId,
            request.Title,
            request.Content,
            request.CoverImageUrl,
            request.IsPublished,
            request.SeoTitle,
            request.SeoDescription);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            postId = result.Value,
            message = "Blog post created successfully."
        });
    }

    [HttpPut("posts/{id:guid}")]
    public async Task<IActionResult> UpdatePost(
        Guid id,
        [FromBody] UpdateBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBlogPostCommand(
            id,
            request.BlogCategoryId,
            request.Title,
            request.Content,
            request.CoverImageUrl,
            request.IsPublished,
            request.SeoTitle,
            request.SeoDescription);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Blog post updated successfully." });
    }

    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteBlogPostCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Blog post deleted successfully." });
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateBlogCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBlogCategoryCommand(request.Name);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            categoryId = result.Value,
            message = "Category created successfully."
        });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "POST_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "POST_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record CreateBlogPostRequest(
    Guid? BlogCategoryId,
    string Title,
    string Content,
    string? CoverImageUrl,
    bool IsPublished,
    string? SeoTitle,
    string? SeoDescription);

public record UpdateBlogPostRequest(
    Guid? BlogCategoryId,
    string Title,
    string Content,
    string? CoverImageUrl,
    bool IsPublished,
    string? SeoTitle,
    string? SeoDescription);

public record CreateBlogCategoryRequest(string Name);