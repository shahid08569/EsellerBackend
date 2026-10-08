using Eseller.Application.Abstractions;
using Eseller.Application.Features.AdminCategories.UpdateCategoryHomepage;
using Eseller.Application.Features.Categories.CreateCategory;
using Eseller.Application.Features.Categories.DeleteCategory;
using Eseller.Application.Features.Categories.DeleteCategoryImage;
using Eseller.Application.Features.Categories.MirrorExternalCategoryImages;
using Eseller.Application.Features.Categories.UpdateCategory;
using Eseller.Application.Features.Categories.UploadCategoryImage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;
    private readonly IRemoteImageIngestService _imageIngest;

    public AdminCategoriesController(
        ISender sender,
        IApplicationDbContext context,
        IRemoteImageIngestService imageIngest)
    {
        _sender = sender;
        _context = context;
        _imageIngest = imageIngest;
    }

    /// <summary>
    /// Get all categories with full details, parent names, product counts, and homepage status (SuperAdmin).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.HomepageDisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var parentIds = categories
            .Where(c => c.ParentCategoryId.HasValue)
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();

        var parentNames = await _context.Categories
            .Where(c => parentIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var categoryIds = categories.Select(c => c.Id).ToList();
        var productCounts = await _context.Products
            .Where(p => categoryIds.Contains(p.CategoryId) && p.IsActive && p.IsApproved && p.DeletedAt == null)
            .GroupBy(p => p.CategoryId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var result = categories.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            slug = c.Slug,
            imageUrl = c.ImageUrl,
            parentId = c.ParentCategoryId,
            parentName = c.ParentCategoryId.HasValue ? parentNames.GetValueOrDefault(c.ParentCategoryId.Value) : null,
            isFeaturedOnHomepage = c.IsFeaturedOnHomepage,
            homepageDisplayOrder = c.HomepageDisplayOrder,
            displayOrder = c.HomepageDisplayOrder,
            isActive = c.IsActive,
            seoTitle = c.SeoTitle,
            seoDescription = c.SeoDescription,
            productCount = productCounts.GetValueOrDefault(c.Id, 0),
            createdAt = c.CreatedAt
        }).ToList();

        return Ok(result);
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

        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == result.Value, cancellationToken);
        if (cat != null)
        {
            if (request.IsFeaturedOnHomepage.HasValue)
            {
                cat.IsFeaturedOnHomepage = request.IsFeaturedOnHomepage.Value;
                cat.HomepageDisplayOrder = request.HomepageDisplayOrder ?? 0;
            }

            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                var imageUrl = request.ImageUrl.Trim();
                try
                {
                    var mirrored = await _imageIngest.IngestCategoryImageAsync(cat.Id, imageUrl, cancellationToken);
                    cat.ImageUrl = mirrored ?? imageUrl;
                }
                catch
                {
                    cat.ImageUrl = imageUrl;
                }
            }

            if (request.IsActive.HasValue)
                cat.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync(cancellationToken);
        }

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

        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cat != null)
        {
            if (request.IsFeaturedOnHomepage.HasValue)
                cat.IsFeaturedOnHomepage = request.IsFeaturedOnHomepage.Value;
            if (request.HomepageDisplayOrder.HasValue)
                cat.HomepageDisplayOrder = request.HomepageDisplayOrder.Value;
            // Admin form always sends imageUrl — null/empty clears the logo.
            // External CDN URLs are mirrored into /uploads so logos always load.
            if (string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                cat.ImageUrl = null;
            }
            else
            {
                var imageUrl = request.ImageUrl.Trim();
                try
                {
                    var mirrored = await _imageIngest.IngestCategoryImageAsync(cat.Id, imageUrl, cancellationToken);
                    cat.ImageUrl = mirrored ?? imageUrl;
                }
                catch
                {
                    cat.ImageUrl = imageUrl;
                }
            }

            if (request.ParentCategoryId.HasValue)
                cat.ParentCategoryId = request.ParentCategoryId;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Category updated successfully." });
    }

    /// <summary>
    /// One-time / on-demand: copy all external category logos into local /uploads.
    /// </summary>
    [HttpPost("mirror-external-images")]
    public async Task<IActionResult> MirrorExternalImages(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MirrorExternalCategoryImagesCommand(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new
        {
            mirrored = result.Value!.Mirrored,
            skipped = result.Value.Skipped,
            failed = result.Value.Failed,
            errors = result.Value.Errors,
            message = $"Mirrored {result.Value.Mirrored} category logo(s) into local storage."
        });
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

    /// <summary>
    /// Toggle or set whether a category appears on the homepage and its display order.
    /// </summary>
    [HttpPut("{id:guid}/homepage")]
    [HttpPost("{id:guid}/homepage")]
    public async Task<IActionResult> UpdateHomepage(
        Guid id,
        [FromBody] UpdateCategoryHomepageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryHomepageCommand(
            id,
            request.IsFeaturedOnHomepage,
            request.HomepageDisplayOrder);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = $"Category homepage visibility set to {request.IsFeaturedOnHomepage} (Order: {request.HomepageDisplayOrder})." });
    }
}

public record CreateCategoryRequest(
    Guid? ParentCategoryId,
    string Name,
    string? SeoTitle,
    string? SeoDescription,
    bool? IsFeaturedOnHomepage = false,
    int? HomepageDisplayOrder = 0,
    string? ImageUrl = null,
    bool? IsActive = true);

public record UpdateCategoryRequest(
    string Name,
    string? SeoTitle,
    string? SeoDescription,
    bool? IsActive = null,
    Guid? ParentCategoryId = null,
    string? ImageUrl = null,
    bool? IsFeaturedOnHomepage = null,
    int? HomepageDisplayOrder = null);

public record UpdateCategoryHomepageRequest(
    bool IsFeaturedOnHomepage,
    int HomepageDisplayOrder);