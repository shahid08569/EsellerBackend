using Eseller.Application.Abstractions;
using Eseller.Application.Features.Shops;
using Eseller.Application.Features.Shops.DeleteShop;
using Eseller.Application.Features.Shops.DeleteShopBanner;
using Eseller.Application.Features.Shops.DeleteShopLogo;
using Eseller.Application.Features.Shops.GetMyShops;
using Eseller.Application.Features.Shops.GetShopById;
using Eseller.Application.Features.Shops.GetShopBySlug;
using Eseller.Application.Features.Shops.GetShops;
using Eseller.Application.Features.Shops.UpdateShop;
using Eseller.Application.Features.Shops.UploadShopBanner;
using Eseller.Application.Features.Shops.UploadShopLogo;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ShopsController : ControllerBase
{
    private readonly ISender _sender;

    public ShopsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get current shopkeeper's own shops.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> GetMy(
        CancellationToken cancellationToken)
    {
        var query = new GetMyShopsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get shop details by slug (public storefront).
    /// </summary>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetShopBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? shopCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetShopsQuery(pageNumber, pageSize, shopCategoryId);
            var result = await _sender.Send(query, cancellationToken);

            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

            return Ok(result.Value);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Dashboard reload / navigation aborts the request — not a fault
            return NoContent();
        }
    }


    /// <summary>
    /// Get shop details by ID (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetShopByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

  
    /// <summary>
    /// Update own shop (authenticated shopkeeper).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShopRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateShopCommand(
            id,
            request.Name,
            request.Description,
            request.Phone,
            request.Address,
            request.City,
            request.Country);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Shop updated successfully." });
    }


    /// <summary>
    /// Upload shop logo (shopkeeper only).
    /// </summary>
    [HttpPost("{id:guid}/logo")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    [RequestSizeLimit(5 * 1024 * 1024)]   // 5 MB
    public async Task<IActionResult> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadShopLogoCommand
        {
            ShopId = id,
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
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { logoUrl = result.Value, message = "Logo uploaded successfully." });
    }

    /// <summary>
    /// Upload shop banner (shopkeeper only).
    /// </summary>
    [HttpPost("{id:guid}/banner")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadBanner(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        using var stream = file.OpenReadStream();
        var command = new UploadShopBannerCommand
        {
            ShopId = id,
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
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { bannerUrl = result.Value, message = "Banner uploaded successfully." });
    }

    /// <summary>
    /// Delete shop logo (shopkeeper only).
    /// </summary>
    [HttpDelete("{id:guid}/logo")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> DeleteLogo(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopLogoCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Logo deleted successfully." });
    }

    /// <summary>
    /// Delete shop banner (shopkeeper only).
    /// </summary>
    [HttpDelete("{id:guid}/banner")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> DeleteBanner(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopBannerCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Banner deleted successfully." });
    }

    /// <summary>
    /// Delete shop (SuperAdmin or owning Shopkeeper).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> DeleteShop(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Shop deleted successfully." });
    }

    /// <summary>
    /// Request shop tier upgrade (e.g. Bronze -> Gold Elite, Diamond VIP).
    /// </summary>
    [HttpPost("tier-upgrade")]
    [Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> RequestTierUpgrade(
        [FromBody] RequestTierUpgradeRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IAppNotificationService notifications,
        CancellationToken cancellationToken)
    {
        if (currentUserService.AccountId == null) return Unauthorized();

        var shopkeeper = await context.Shopkeepers
            .FirstOrDefaultAsync(s => s.AccountId == currentUserService.AccountId.Value, cancellationToken);
        if (shopkeeper == null) return NotFound(new { error = "Shopkeeper profile not found." });

        var shop = await context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null, cancellationToken);
        if (shop == null) return NotFound(new { error = "Shop not found." });

        var categories = await context.ShopCategories.Where(sc => sc.DeletedAt == null).ToListAsync(cancellationToken);

        ShopCategory? targetCat = null;
        if (request.RequestedCategoryId is Guid catId && catId != Guid.Empty)
            targetCat = categories.FirstOrDefault(c => c.Id == catId);

        targetCat ??= categories.FirstOrDefault(c =>
            (!string.IsNullOrWhiteSpace(request.RequestedTier) && (
                string.Equals(c.Name, request.RequestedTier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.BadgeText, request.RequestedTier, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(request.RequestedTier, StringComparison.OrdinalIgnoreCase) ||
                request.RequestedTier.Contains(c.Name, StringComparison.OrdinalIgnoreCase))) ||
            (!string.IsNullOrWhiteSpace(c.BadgeText) && !string.IsNullOrWhiteSpace(request.RequestedTier) && (
                c.BadgeText.Contains(request.RequestedTier, StringComparison.OrdinalIgnoreCase) ||
                request.RequestedTier.Contains(c.BadgeText, StringComparison.OrdinalIgnoreCase))));

        if (targetCat == null)
            return BadRequest(new { error = $"Unknown package '{request.RequestedTier}'. Pick a plan from the list.", errorCode = "TIER_NOT_FOUND" });

        // Block duplicate pending request for same shop + tier
        var hasPending = await context.TierUpgradeRequests.AnyAsync(r =>
            r.ShopId == shop.Id
            && r.Status == "Pending"
            && (r.RequestedCategoryId == targetCat.Id
                || r.RequestedTier == targetCat.Name
                || r.RequestedTier == targetCat.BadgeText), cancellationToken);
        if (hasPending)
            return BadRequest(new { error = "You already have a pending request for this package. Wait for Super Admin review.", errorCode = "TIER_REQUEST_PENDING" });

        var price = targetCat.PriceUsd > 0 ? targetCat.PriceUsd : request.Price;
        if (price > 0 && string.IsNullOrWhiteSpace(request.ReceiptUrl))
            return BadRequest(new { error = "Payment receipt is required for paid package upgrades.", errorCode = "RECEIPT_REQUIRED" });

        var currentCat = shop.ShopCategoryId.HasValue
            ? categories.FirstOrDefault(c => c.Id == shop.ShopCategoryId.Value)
            : null;
        var currentLabel = !string.IsNullOrWhiteSpace(shop.CustomBadgeText)
            ? shop.CustomBadgeText!
            : (currentCat?.BadgeText ?? currentCat?.Name ?? "Bronze (Free)");

        var tierReq = new Eseller.Domain.Entities.Catalog.TierUpgradeRequest
        {
            ShopId = shop.Id,
            ShopName = shop.Name,
            MerchantName = shopkeeper.Name,
            CurrentTier = currentLabel,
            RequestedTier = targetCat.Name,
            RequestedCategoryId = targetCat.Id,
            BadgeText = targetCat.BadgeText ?? targetCat.Name,
            Price = price,
            PaymentMethod = request.PaymentMethod ?? "Raast / Online Bank Transfer",
            ReferenceNote = request.ReferenceNote ?? string.Empty,
            ReceiptUrl = request.ReceiptUrl,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        tierReq.SetCreatedAt(DateTime.UtcNow);

        context.TierUpgradeRequests.Add(tierReq);
        await context.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAdminsAsync(
            NotificationType.TierUpdated,
            "Pending Tier Upgrade Request",
            $"{shop.Name} requested upgrade to {targetCat.Name}. Open Tier Requests to Approve or Reject.",
            NotificationEntityType.TierRequest,
            tierReq.Id,
            "/shops?view=tiers",
            $"TierUpdated:{tierReq.Id:N}",
            cancellationToken);

        return Ok(new { message = "Tier upgrade request submitted for Super Admin review.", requestId = tierReq.Id });
    }

    /// <summary>
    /// Record storefront visit by slug (public auto-increment).
    /// </summary>
    [HttpPost("{slug}/visit")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordVisitBySlug(
        string slug,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var shop = await context.Shops
            .FirstOrDefaultAsync(s => s.Slug == slug && s.DeletedAt == null, cancellationToken);
        if (shop == null) return NotFound(new { error = "Shop not found." });

        return await RecordVisitInternal(shop.Id, context, cancellationToken);
    }

    /// <summary>
    /// Record storefront visit by shop ID (public auto-increment).
    /// </summary>
    [HttpPost("{id:guid}/visit")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordVisitById(
        Guid id,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var shop = await context.Shops
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, cancellationToken);
        if (shop == null) return NotFound(new { error = "Shop not found." });

        return await RecordVisitInternal(shop.Id, context, cancellationToken);
    }

    private async Task<IActionResult> RecordVisitInternal(
        Guid shopId,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var record = await context.ShopTraffics
            .FirstOrDefaultAsync(t => t.ShopId == shopId && t.Date == todayUtc, cancellationToken);

        if (record == null)
        {
            record = new ShopTraffic
            {
                ShopId = shopId,
                Date = todayUtc,
                VisitCount = 1,
                UniqueVisitors = 1,
                IsManual = false
            };
            record.SetCreatedAt(DateTime.UtcNow);
            context.ShopTraffics.Add(record);
        }
        else
        {
            record.VisitCount += 1;
            record.UniqueVisitors += 1;
            record.SetUpdatedAt(DateTime.UtcNow);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, shopId, visitsToday = record.VisitCount });
    }

    /// <summary>
    /// Get seller's shop traffic analytics and visitor timeline.
    /// </summary>
    [HttpGet("my/traffic")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> GetMyTraffic(
        [FromQuery] string range = "30d",
        [FromServices] IApplicationDbContext context = null!,
        [FromServices] ICurrentUserService currentUserService = null!,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.AccountId == null) return Unauthorized();

        var shopkeeper = await context.Shopkeepers
            .FirstOrDefaultAsync(s => s.AccountId == currentUserService.AccountId.Value, cancellationToken);
        if (shopkeeper == null) return NotFound(new { error = "Shopkeeper profile not found." });

        var shop = await context.Shops
            .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null, cancellationToken);
        if (shop == null) return NotFound(new { error = "Shop not found." });

        var summary = await ShopTrafficHelper.BuildSummaryAsync(shop.Id, shop.Name, range, context, cancellationToken);
        return Ok(summary);
    }
}

public record UpdateShopRequest(
    string Name,
    string? Description,
    string? Phone,
    string? Address,
    string? City,
    string? Country);

public record RequestTierUpgradeRequest(
    string RequestedTier,
    decimal Price,
    string? PaymentMethod,
    string? ReferenceNote,
    string? ReceiptUrl,
    Guid? RequestedCategoryId = null);