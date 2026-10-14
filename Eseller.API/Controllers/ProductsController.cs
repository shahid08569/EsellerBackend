using Eseller.Application.Features.Products.AddCatalogProductToShop;
using Eseller.Application.Features.Products.CreateProduct;
using Eseller.Application.Features.Products.DeleteAllProductImages;
using Eseller.Application.Features.Products.DeleteProduct;
using Eseller.Application.Features.Products.DeleteProductImage;
using Eseller.Application.Features.Products.GetCatalogProducts;
using Eseller.Application.Features.Products.GetMyProducts;
using Eseller.Application.Features.Products.GetProductById;
using Eseller.Application.Features.Products.GetProductBySlug;
using Eseller.Application.Features.Products.GetProductImages;
using Eseller.Application.Features.Products.GetProducts;
using Eseller.Application.Features.Products.SetProductImageCover;
using Eseller.Application.Features.Products.UpdateProduct;
using Eseller.Application.Features.Products.UpdateProductImageSort;
using Eseller.Application.Features.Products.UploadMultipleProductImages;
using Eseller.Application.Features.Products.UploadProductImage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender)
    {
        _sender = sender;
    }

    // ─────────────────────────────────────────────────────────────
    // PUBLIC ENDPOINTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Get products with filters and pagination (public).
    /// Only returns Approved + Active products.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? shopId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minRating,
        [FromQuery] string? search,
        [FromQuery] bool? isFeatured,
        [FromQuery] string sortBy = "newest",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductsQuery(
            categoryId, brandId, shopId,
            minPrice, maxPrice, minRating,
            search, isFeatured, sortBy,
            pageNumber, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get product by slug (public).
    /// </summary>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetProductBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get product by ID (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetProductByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    // ─────────────────────────────────────────────────────────────
    // SHOPKEEPER ENDPOINTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Super Admin master catalog for sellers to browse and Add to Store.
    /// </summary>
    [HttpGet("catalog")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] string? search,
        [FromQuery] string sortBy = "newest",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCatalogProductsQuery(categoryId, brandId, search, sortBy, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Seller requests to list a Super Admin catalog product on their shop (pending approval).
    /// </summary>
    [HttpPost("add-to-store")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> AddToStore(
        [FromBody] AddToStoreRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddCatalogProductToShopCommand(request.SourceProductId, request.ShopId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" or "CATALOG_PRODUCT_NOT_FOUND" =>
                    NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_ALREADY_REQUESTED" =>
                    Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                "STORE_NOT_VERIFIED" or "PRODUCT_LIMIT_REACHED" or "FORBIDDEN" =>
                    StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            productId = result.Value,
            isPendingApproval = true,
            status = "pending_approval",
            message = "Listing request submitted. Status: Approval In Progress. The product will go live on your shop after Super Admin approval."
        });
    }

    /// <summary>
    /// Get shopkeeper's own products (including pending + rejected).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> GetMy(
        [FromQuery] Guid shopId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyProductsQuery(shopId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Create product — SuperAdmin → master warehouse; Shopkeeper → own shop (pending approval).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(
            request.ShopId, request.CategoryId, request.BrandId,
            request.Name, request.Description, request.BasePrice,
            request.SeoTitle, request.SeoDescription);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOPKEEPER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "BRAND_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_VERIFIED" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "PRODUCT_LIMIT_REACHED" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        var isSeller = User.IsInRole("Shopkeeper");
        return Ok(new
        {
            productId = result.Value,
            isPendingApproval = isSeller,
            message = isSeller
                ? "Product submitted for Super Admin approval."
                : "Catalog product created successfully."
        });
    }

    /// <summary>
    /// Update a product (shopkeeper own / admin any).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(
            id, request.CategoryId, request.BrandId,
            request.Name, request.Description, request.BasePrice,
            request.SeoTitle, request.SeoDescription);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "BRAND_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product updated successfully." });
    }

    /// <summary>
    /// Soft delete a product (shopkeeper own / admin any).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteProductCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product deleted successfully." });
    }

    // ─────────────────────────────────────────────────────────────
    // PRODUCT IMAGES
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Get all images for a product (public).
    /// </summary>
    [HttpGet("{id:guid}/images")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImages(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetProductImagesQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Set cover image (Super Admin any product; Shopkeeper own shop products).
    /// </summary>
    [HttpPut("{id:guid}/images/{imageId:guid}/cover")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> SetCoverImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetProductImageCoverCommand(id, imageId), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" or "IMAGE_NOT_FOUND" =>
                    NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Cover image updated successfully." });
    }

    /// <summary>
    /// Upload a product image (admin catalog or seller own product).
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadProductImageCommand
        {
            ProductId = id,
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
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { imageId = result.Value, message = "Image uploaded successfully." });
    }

    /// <summary>
    /// Upload multiple product images at once (admin catalog or seller own product).
    /// </summary>
    [HttpPost("{id:guid}/images/bulk")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    [RequestSizeLimit(50 * 1024 * 1024)]   // 50 MB total
    public async Task<IActionResult> UploadMultipleImages(
        Guid id,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var fileList = (files != null && files.Count > 0)
            ? files
            : (Request.HasFormContentType && Request.Form.Files.Count > 0)
                ? Request.Form.Files.ToList()
                : new List<IFormFile>();

        if (fileList.Count == 0)
            return BadRequest(new { error = "At least one file is required.", errorCode = "NO_FILES" });

        var fileStreams = new List<ProductImageFile>();
        var streamsToDispose = new List<Stream>();

        try
        {
            foreach (var file in fileList)
            {
                var stream = file.OpenReadStream();
                streamsToDispose.Add(stream);

                fileStreams.Add(new ProductImageFile
                {
                    FileStream = stream,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length
                });
            }

            var command = new UploadMultipleProductImagesCommand
            {
                ProductId = id,
                Files = fileStreams
            };

            var result = await _sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                    "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                    _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
                };
            }

            return Ok(new
            {
                imageIds = result.Value,
                count = result.Value!.Count,
                message = $"{result.Value.Count} images uploaded successfully."
            });
        }
        finally
        {
            foreach (var stream in streamsToDispose)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Update image sort order (shopkeeper own / admin any).
    /// </summary>
    [HttpPut("{id:guid}/images/{imageId:guid}/sort")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> UpdateImageSort(
        Guid id,
        Guid imageId,
        [FromBody] UpdateImageSortRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductImageSortCommand(id, imageId, request.SortOrder);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "IMAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Image sort order updated successfully." });
    }

    /// <summary>
    /// Delete a product image (shopkeeper own / admin any).
    /// </summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> DeleteImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteProductImageCommand(id, imageId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "IMAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Image deleted successfully." });
    }

    /// <summary>
    /// Delete all product images (shopkeeper own / admin any).
    /// </summary>
    [HttpDelete("{id:guid}/images")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> DeleteAllImages(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteAllProductImagesCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "All images deleted successfully." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record CreateProductRequest(
    Guid? ShopId,
    Guid CategoryId,
    Guid? BrandId,
    string Name,
    string? Description,
    decimal BasePrice,
    string? SeoTitle,
    string? SeoDescription);

public record UpdateProductRequest(
    Guid CategoryId,
    Guid? BrandId,
    string Name,
    string? Description,
    decimal BasePrice,
    string? SeoTitle,
    string? SeoDescription);

public record AddToStoreRequest(Guid SourceProductId, Guid ShopId);

public record UpdateImageSortRequest(int SortOrder);