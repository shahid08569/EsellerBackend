using Eseller.Application.Abstractions;
using Eseller.Application.Features.Products;
using Eseller.Application.Features.Products.ApproveProduct;
using Eseller.Application.Features.Products.GetApprovedSellerProducts;
using Eseller.Application.Features.Products.GetPendingProducts;
using Eseller.Application.Features.Products.GetRejectedProducts;
using Eseller.Application.Features.Products.RejectProduct;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminProductsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;

    public AdminProductsController(ISender sender, IApplicationDbContext context)
    {
        _sender = sender;
        _context = context;
    }

    /// <summary>
    /// Get pending products for approval.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        /// <summary>global|warehouse = warehouse listing requests; own|seller = seller-created products.</summary>
        [FromQuery] string? kind = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPendingProductsQuery(pageNumber, pageSize, kind);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get rejected seller listing requests.
    /// </summary>
    [HttpGet("rejected")]
    public async Task<IActionResult> GetRejected(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetRejectedProductsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get approved seller store listings (live on merchant shops).
    /// </summary>
    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        /// <summary>global|warehouse|listing = warehouse clones; own|seller|new = seller-created.</summary>
        [FromQuery] string? kind = null,
        [FromQuery] Guid? shopId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetApprovedSellerProductsQuery(pageNumber, pageSize, kind, shopId, categoryId, brandId, search);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Approve a product.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveProductCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product approved successfully." });
    }

    /// <summary>
    /// Approve multiple pending products at once.
    /// </summary>
    [HttpPost("approve-bulk")]
    public async Task<IActionResult> ApproveBulk(
        [FromBody] BulkProductIdsRequest request,
        CancellationToken cancellationToken)
    {
        var ids = (request.ProductIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return BadRequest(new { error = "No product IDs provided.", errorCode = "VALIDATION_ERROR" });

        var approved = 0;
        var failed = new List<object>();

        foreach (var id in ids)
        {
            var result = await _sender.Send(new ApproveProductCommand(id), cancellationToken);
            if (result.IsSuccess) approved++;
            else failed.Add(new { productId = id, error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(new
        {
            message = $"{approved} product(s) approved successfully.",
            approved,
            failedCount = failed.Count,
            failed
        });
    }

    /// <summary>
    /// Bulk set Featured on master catalog products.
    /// </summary>
    [HttpPut("featured-bulk")]
    public async Task<IActionResult> SetFeaturedBulk(
        [FromBody] BulkFeaturedRequest request,
        CancellationToken cancellationToken)
    {
        var ids = (request.ProductIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { error = "No product IDs provided.", errorCode = "VALIDATION_ERROR" });

        var updated = 0;
        foreach (var id in ids)
        {
            var product = await FindCatalogProductAsync(id, cancellationToken);
            if (product is null) continue;
            product.IsFeatured = request.IsFeatured;
            if (request.IsFeatured)
                product.IsStorefrontLive = true;
            product.SetUpdatedAt(DateTime.UtcNow);
            await PropagateFeaturedFlagAsync(product.Id, request.IsFeatured, cancellationToken);
            updated++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            message = request.IsFeatured
                ? $"{updated} product(s) added to Featured."
                : $"{updated} product(s) removed from Featured.",
            updated
        });
    }

    /// <summary>
    /// Bulk set Hot Selling on master catalog products.
    /// </summary>
    [HttpPut("hot-selling-bulk")]
    public async Task<IActionResult> SetHotSellingBulk(
        [FromBody] BulkHotSellingRequest request,
        CancellationToken cancellationToken)
    {
        var ids = (request.ProductIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { error = "No product IDs provided.", errorCode = "VALIDATION_ERROR" });

        var updated = 0;
        foreach (var id in ids)
        {
            var product = await FindCatalogProductAsync(id, cancellationToken);
            if (product is null) continue;
            product.IsHotSelling = request.IsHotSelling;
            if (request.IsHotSelling)
                product.IsStorefrontLive = true;
            product.SetUpdatedAt(DateTime.UtcNow);
            await PropagateHotSellingFlagAsync(product.Id, request.IsHotSelling, cancellationToken);
            updated++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            message = request.IsHotSelling
                ? $"{updated} product(s) added to Hot Selling."
                : $"{updated} product(s) removed from Hot Selling.",
            updated
        });
    }

    /// <summary>
    /// Reject a product with a reason.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectProductCommand(id, request.Reason);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Product rejected successfully." });
    }

    /// <summary>
    /// Catalog products for homepage merchandising (featured / hot selling / pick list).
    /// </summary>
    [HttpGet("homepage")]
    public async Task<IActionResult> GetHomepageCatalog(
        [FromQuery] string? tab = "featured",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 40,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageNumber < 1) pageNumber = 1;

        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);
        var mode = (string.IsNullOrWhiteSpace(tab) ? "featured" : tab).Trim().ToLowerInvariant();

        // All Platform Master Warehouse catalog SKUs — SuperAdmin can list any existing
        // product on the customer website without waiting for a seller "Add to Store".
        IQueryable<Domain.Entities.Catalog.Product> baseQuery = _context.Products
            .IgnoreQueryFilters()
            .Where(p =>
                p.ShopId == masterShop.Id
                && p.SourceProductId == null
                && p.DeletedAt == null
                && p.IsActive
                && p.IsApproved);

        IQueryable<Domain.Entities.Catalog.Product> query;
        if (mode == "featured")
        {
            query = baseQuery.Where(p => p.IsFeatured).OrderByDescending(p => p.UpdatedAt);
        }
        else if (mode is "hot" or "hotselling" or "hot-selling")
        {
            query = baseQuery.Where(p => p.IsHotSelling).OrderByDescending(p => p.UpdatedAt);
        }
        else
        {
            query = baseQuery
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.IsHotSelling)
                .ThenByDescending(p => p.ViewCount)
                .ThenByDescending(p => p.CreatedAt);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.IsFeatured,
                p.IsHotSelling,
                p.IsStorefrontLive,
                p.ViewCount,
                p.AvgRating,
                p.IsActive,
                p.CreatedAt,
                CoverImageUrl = _context.ProductImages
                    .Where(i => i.ProductId == p.Id)
                    .OrderByDescending(i => i.IsCover)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items = page, totalCount, pageNumber, pageSize, tab = mode });
    }

    /// <summary>
    /// Toggle Master Warehouse product visibility on the customer website.
    /// Off = warehouse / seller catalog only; On = also live as Platform Master Warehouse.
    /// </summary>
    [HttpPut("{id:guid}/storefront-live")]
    public async Task<IActionResult> SetStorefrontLive(
        Guid id,
        [FromBody] SetStorefrontLiveRequest request,
        CancellationToken cancellationToken)
    {
        var product = await FindCatalogProductAsync(id, cancellationToken);
        if (product is null)
            return NotFound(new { error = "Catalog product not found.", errorCode = "PRODUCT_NOT_FOUND" });

        product.IsStorefrontLive = request.IsStorefrontLive;
        if (!request.IsStorefrontLive)
        {
            // Hide from homepage pins when taken offline
            product.IsFeatured = false;
            product.IsHotSelling = false;
            await PropagateFeaturedFlagAsync(product.Id, false, cancellationToken);
            await PropagateHotSellingFlagAsync(product.Id, false, cancellationToken);
        }

        product.SetUpdatedAt(DateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = request.IsStorefrontLive
                ? "Product is now live on the customer website (Warehouse)."
                : "Product removed from customer website (Warehouse only).",
            productId = product.Id,
            isStorefrontLive = product.IsStorefrontLive,
            status = product.IsStorefrontLive ? "Warehouse · Live" : "Warehouse"
        });
    }

    /// <summary>
    /// Toggle master catalog product as homepage Featured.
    /// </summary>
    [HttpPut("{id:guid}/featured")]
    public async Task<IActionResult> SetFeatured(
        Guid id,
        [FromBody] SetFeaturedRequest request,
        CancellationToken cancellationToken)
    {
        var product = await FindCatalogProductAsync(id, cancellationToken);
        if (product is null)
            return NotFound(new { error = "Catalog product not found.", errorCode = "PRODUCT_NOT_FOUND" });

        product.IsFeatured = request.IsFeatured;
        if (request.IsFeatured)
            product.IsStorefrontLive = true; // Featured implies live on website
        product.SetUpdatedAt(DateTime.UtcNow);
        await PropagateFeaturedFlagAsync(product.Id, request.IsFeatured, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = request.IsFeatured
                ? "Product marked as Featured for homepage."
                : "Product removed from Featured.",
            productId = product.Id,
            isFeatured = product.IsFeatured,
            isStorefrontLive = product.IsStorefrontLive
        });
    }

    /// <summary>
    /// Toggle master catalog product as homepage Hot Selling.
    /// </summary>
    [HttpPut("{id:guid}/hot-selling")]
    public async Task<IActionResult> SetHotSelling(
        Guid id,
        [FromBody] SetHotSellingRequest request,
        CancellationToken cancellationToken)
    {
        var product = await FindCatalogProductAsync(id, cancellationToken);
        if (product is null)
            return NotFound(new { error = "Catalog product not found.", errorCode = "PRODUCT_NOT_FOUND" });

        product.IsHotSelling = request.IsHotSelling;
        if (request.IsHotSelling)
            product.IsStorefrontLive = true;
        product.SetUpdatedAt(DateTime.UtcNow);
        await PropagateHotSellingFlagAsync(product.Id, request.IsHotSelling, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = request.IsHotSelling
                ? "Product added to Hot Selling."
                : "Product removed from Hot Selling.",
            productId = product.Id,
            isHotSelling = product.IsHotSelling
        });
    }

    private async Task PropagateFeaturedFlagAsync(
        Guid catalogProductId,
        bool isFeatured,
        CancellationToken cancellationToken)
    {
        var listings = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.SourceProductId == catalogProductId && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var listing in listings)
        {
            listing.IsFeatured = isFeatured;
            listing.SetUpdatedAt(DateTime.UtcNow);
        }
    }

    private async Task PropagateHotSellingFlagAsync(
        Guid catalogProductId,
        bool isHotSelling,
        CancellationToken cancellationToken)
    {
        var listings = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.SourceProductId == catalogProductId && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var listing in listings)
        {
            listing.IsHotSelling = isHotSelling;
            listing.SetUpdatedAt(DateTime.UtcNow);
        }
    }

    private async Task<Domain.Entities.Catalog.Product?> FindCatalogProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);
        return await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.Id == id
                && p.ShopId == masterShop.Id
                && p.SourceProductId == null
                && p.DeletedAt == null, cancellationToken);
    }
}

public record RejectProductRequest(string Reason);
public record SetFeaturedRequest(bool IsFeatured);
public record SetHotSellingRequest(bool IsHotSelling);
public record SetStorefrontLiveRequest(bool IsStorefrontLive);
public record BulkProductIdsRequest(Guid[]? ProductIds);
public record BulkFeaturedRequest(Guid[]? ProductIds, bool IsFeatured);
public record BulkHotSellingRequest(Guid[]? ProductIds, bool IsHotSelling);