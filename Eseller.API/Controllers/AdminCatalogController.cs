using Eseller.Application.Features.AdminCatalog.PurgeCatalog;
using Eseller.Application.Features.AdminCatalog.SeedProductReviews;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/catalog")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminCatalogController : ControllerBase
{
    private readonly ISender _sender;

    public AdminCatalogController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Soft-delete all products, brands, and categories (SuperAdmin only).
    /// </summary>
    [HttpPost("purge")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Purge(
        [FromBody] PurgeCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PurgeCatalogCommand(request.Confirm ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden,
                    new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            message = "Catalog purged.",
            productsDeleted = result.Value!.ProductsDeleted,
            brandsDeleted = result.Value.BrandsDeleted,
            categoriesDeleted = result.Value.CategoriesDeleted
        });
    }

    /// <summary>
    /// Seed approved dummy reviews on a product (creates import reviewers as needed).
    /// </summary>
    [HttpPost("products/{productId:guid}/seed-reviews")]
    public async Task<IActionResult> SeedReviews(
        Guid productId,
        [FromBody] SeedProductReviewsRequest request,
        CancellationToken cancellationToken)
    {
        var items = (request.Reviews ?? Array.Empty<SeedReviewItemDto>())
            .Select(r => new SeedReviewItem(r.ReviewerName, r.Rating, r.Title, r.Comment))
            .ToList();

        var result = await _sender.Send(
            new SeedProductReviewsCommand(productId, items),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden,
                    new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { created = result.Value, message = "Reviews seeded." });
    }
}

public record PurgeCatalogRequest(string? Confirm);

public record SeedProductReviewsRequest(SeedReviewItemDto[]? Reviews);

public record SeedReviewItemDto(
    string ReviewerName,
    int Rating,
    string? Title,
    string? Comment);
