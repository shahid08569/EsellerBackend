using Eseller.Application.Features.Categories.CreateCategory;
using Eseller.Application.Features.Categories.DeleteCategory;
using Eseller.Application.Features.Categories.DeleteCategoryImage;
using Eseller.Application.Features.Categories.UpdateCategory;
using Eseller.Application.Features.Categories.UploadCategoryImage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminCategoriesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create a new category.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCategoryCommand(
            request.ParentCategoryId,
            request.Name,
            request.SeoTitle,
            request.SeoDescription);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { categoryId = result.Value, message = "Category created successfully." });
    }

    /// <summary>
    /// Update a category.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryCommand(
            id,
            request.Name,
            request.SeoTitle,
            request.SeoDescription,
            request.IsActive);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Category updated successfully." });
    }

    /// <summary>
    /// Soft delete a category.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCategoryCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Category deleted successfully." });
    }

    /// <summary>
    /// Upload category image.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadCategoryImageCommand
        {
            CategoryId = id,
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length
        };

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { imageUrl = result.Value, message = "Image uploaded successfully." });
    }

    /// <summary>
    /// Delete category image.
    /// </summary>
    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> DeleteImage(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCategoryImageCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Image deleted successfully." });
    }
}

public record CreateCategoryRequest(
    Guid? ParentCategoryId,
    string Name,
    string? SeoTitle,
    string? SeoDescription);

public record UpdateCategoryRequest(
    string Name,
    string? SeoTitle,
    string? SeoDescription,
    bool IsActive);