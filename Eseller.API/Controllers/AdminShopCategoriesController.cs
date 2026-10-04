using Eseller.Application.Abstractions;
using Eseller.Application.Features.ShopCategories.CreateShopCategory;
using Eseller.Application.Features.ShopCategories.DeleteShopCategory;
using Eseller.Application.Features.ShopCategories.GetShopCategories;
using Eseller.Application.Features.ShopCategories.UpdateShopCategory;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/shop-categories")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminShopCategoriesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;

    public AdminShopCategoriesController(ISender sender, IApplicationDbContext context)
    {
        _sender = sender;
        _context = context;
    }

    /// <summary>
    /// Get all shop categories/tiers with shop counts (Admin).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetShopCategoriesQuery(onlyActive);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Create a new shop category/tier (e.g. Diamond, Gold, Silver, Premium).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateShopCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateShopCategoryCommand(
            request.Name,
            request.Slug,
            request.Description,
            request.BadgeText,
            request.BadgeColor,
            request.IconUrl,
            request.DisplayOrder,
            request.PriceUsd,
            request.MaxProductListings);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { categoryId = result.Value, message = "Shop category created successfully." });
    }

    /// <summary>
    /// Update an existing shop category/tier.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShopCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateShopCategoryCommand(
            id,
            request.Name,
            request.Slug,
            request.Description,
            request.BadgeText,
            request.BadgeColor,
            request.IconUrl,
            request.DisplayOrder,
            request.IsActive,
            request.PriceUsd,
            request.MaxProductListings);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Shop category updated successfully." });
    }

    /// <summary>
    /// Delete a shop category/tier.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteShopCategoryCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Shop category deleted successfully." });
    }

    /// <summary>
    /// Direct tier assignment is disabled. Sellers must apply; Super Admin only approves/rejects requests.
    /// </summary>
    [HttpPut("shops/{shopId:guid}/assign")]
    [HttpPost("shops/{shopId:guid}/assign")]
    public IActionResult AssignToShop(Guid shopId)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "Super Admin cannot assign tiers directly. Sellers must submit a tier upgrade request with payment receipt; then approve or reject it.",
            errorCode = "DIRECT_TIER_ASSIGN_DISABLED",
            shopId
        });
    }

    /// <summary>
    /// Seed / sync standard seller tiers: Bronze Free, Gold, Diamond.
    /// </summary>
    [HttpPost("seed-tiers")]
    public async Task<IActionResult> SeedStandardTiers(CancellationToken cancellationToken)
    {
        // Insert-only: never overwrite admin-edited tier prices/limits
        var specs = new[]
        {
            new { Name = "Bronze (Free)", Slug = "bronze-free", Price = 0m, Limit = 200, Badge = "Bronze (Free)", Color = "#94A3B8", Order = 1 },
            new { Name = "Gold", Slug = "gold", Price = 1000m, Limit = 1000, Badge = "Gold", Color = "#F59E0B", Order = 2 },
            new { Name = "Diamond", Slug = "diamond", Price = 2000m, Limit = 5000, Badge = "Diamond", Color = "#06B6D4", Order = 3 }
        };

        var createdCount = 0;
        foreach (var spec in specs)
        {
            var exists = await _context.ShopCategories
                .AnyAsync(c => c.DeletedAt == null && (c.Slug == spec.Slug || c.Name == spec.Name), cancellationToken);

            if (exists)
                continue;

            _context.ShopCategories.Add(new ShopCategory
            {
                Name = spec.Name,
                Slug = spec.Slug,
                Description = $"{spec.Name} package — up to {spec.Limit} products.",
                BadgeText = spec.Badge,
                BadgeColor = spec.Color,
                IconUrl = "⭐",
                DisplayOrder = spec.Order,
                PriceUsd = spec.Price,
                MaxProductListings = spec.Limit,
                IsActive = true
            });
            createdCount++;
        }

        // Always sync Diamond capacity to 5000 if an older seed left it at 2000
        var diamond = await _context.ShopCategories
            .FirstOrDefaultAsync(c => c.DeletedAt == null && (c.Slug == "diamond" || c.Name == "Diamond"), cancellationToken);
        if (diamond is not null && diamond.MaxProductListings < 5000)
        {
            diamond.MaxProductListings = 5000;
            diamond.PriceUsd = diamond.PriceUsd <= 0 ? 2000m : diamond.PriceUsd;
            diamond.Description = "Diamond package — up to 5000 products.";
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            message = $"Standard tiers ready: Bronze Free ($0/200), Gold ($1000/1000), Diamond ($2000/5000). Created {createdCount} missing tier(s)."
        });
    }

    /// <summary>
    /// Get all tier upgrade requests (from DB).
    /// </summary>
    [HttpGet("tier-requests")]
    public async Task<IActionResult> GetTierRequests(CancellationToken cancellationToken)
    {
        // Remove legacy demo seeds that had empty ShopId (approve/deny appeared broken)
        var orphanSeeds = await _context.TierUpgradeRequests
            .Where(r => r.ShopId == Guid.Empty
                && (r.ShopName == "Metro Deals Direct" || r.ShopName == "PrimeTech Electronics"))
            .ToListAsync(cancellationToken);

        if (orphanSeeds.Count > 0)
        {
            _context.TierUpgradeRequests.RemoveRange(orphanSeeds);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var list = await _context.TierUpgradeRequests
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    /// <summary>
    /// Approve tier upgrade request (updates DB & notifies shopkeeper).
    /// </summary>
    [HttpPost("tier-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveTierRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var req = await _context.TierUpgradeRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (req == null) return NotFound(new { error = "Tier upgrade request not found." });

        if (req.ShopId == Guid.Empty)
            return BadRequest(new { error = "This tier request has no linked shop and cannot be approved." });

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == req.ShopId && s.DeletedAt == null, cancellationToken);

        if (shop == null && !string.IsNullOrWhiteSpace(req.ShopName))
        {
            shop = await _context.Shops.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.DeletedAt == null && s.Name.ToLower() == req.ShopName.ToLower(), cancellationToken);
        }

        if (shop == null)
            return BadRequest(new { error = $"Shop linked to this request was not found (ShopId: {req.ShopId})." });

        req.Status = "Approved";
        req.ReviewedAt = DateTime.UtcNow;

        var categories = await _context.ShopCategories.Where(sc => sc.DeletedAt == null).ToListAsync(cancellationToken);
        ShopCategory? matchedCat = null;
        if (req.RequestedCategoryId.HasValue)
            matchedCat = categories.FirstOrDefault(c => c.Id == req.RequestedCategoryId.Value);

        matchedCat ??= categories.FirstOrDefault(c =>
            (!string.IsNullOrWhiteSpace(c.Name) && (
                c.Name.Contains(req.RequestedTier, StringComparison.OrdinalIgnoreCase) ||
                req.RequestedTier.Contains(c.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.BadgeText) && (
                    c.BadgeText.Contains(req.RequestedTier, StringComparison.OrdinalIgnoreCase) ||
                    req.RequestedTier.Contains(c.BadgeText, StringComparison.OrdinalIgnoreCase))))) ||
            (!string.IsNullOrWhiteSpace(req.BadgeText) && !string.IsNullOrWhiteSpace(c.BadgeText) &&
                c.BadgeText.Equals(req.BadgeText, StringComparison.OrdinalIgnoreCase)));

        shop.ShopCategoryId = matchedCat?.Id ?? req.RequestedCategoryId;
        shop.CustomBadgeText = req.BadgeText ?? matchedCat?.BadgeText ?? req.RequestedTier;
        shop.SetUpdatedAt(DateTime.UtcNow);

        var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);
        if (shopkeeper != null)
        {
            var notif = new Notification
            {
                AccountId = shopkeeper.AccountId,
                Type = NotificationType.TierUpgradeApproved,
                Title = "Store Package Upgraded!",
                Message = $"Congratulations! Your store upgrade request to {req.RequestedTier} has been approved by Super Admin.",
                IsRead = false
            };
            notif.SetCreatedAt(DateTime.UtcNow);
            _context.Notifications.Add(notif);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = $"Tier upgrade for '{req.ShopName}' successfully approved and persisted." });
    }

    /// <summary>
    /// Reject tier upgrade request (updates DB & notifies shopkeeper).
    /// </summary>
    [HttpPost("tier-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectTierRequest(
        Guid id,
        [FromBody] RejectTierUpgradeRequest body,
        CancellationToken cancellationToken)
    {
        var req = await _context.TierUpgradeRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (req == null) return NotFound(new { error = "Tier upgrade request not found." });

        req.Status = "Rejected";
        req.ReviewNotes = body.Reason ?? "Payment verification could not be completed.";
        req.ReviewedAt = DateTime.UtcNow;

        if (req.ShopId != Guid.Empty)
        {
            var shop = await _context.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == req.ShopId, cancellationToken);
            if (shop != null)
            {
                var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);
                if (shopkeeper != null)
                {
                    var notif = new Notification
                    {
                        AccountId = shopkeeper.AccountId,
                        Type = NotificationType.TierUpgradeRejected,
                        Title = "Store Package Upgrade Rejected",
                        Message = $"Your package upgrade request for {req.RequestedTier} was rejected. Note: {req.ReviewNotes}",
                        IsRead = false
                    };
                    notif.SetCreatedAt(DateTime.UtcNow);
                    _context.Notifications.Add(notif);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = $"Tier upgrade request for '{req.ShopName}' marked as Rejected." });
    }

    /// <summary>
    /// Delete / Dismiss tier upgrade request.
    /// </summary>
    [HttpDelete("tier-requests/{id:guid}")]
    public async Task<IActionResult> DeleteTierRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var req = await _context.TierUpgradeRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (req != null)
        {
            _context.TierUpgradeRequests.Remove(req);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return Ok(new { message = "Tier upgrade request removed." });
    }
}

public record CreateShopCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    string? BadgeText,
    string? BadgeColor,
    string? IconUrl,
    int DisplayOrder = 0,
    decimal PriceUsd = 0,
    int MaxProductListings = 200);

public record UpdateShopCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    string? BadgeText,
    string? BadgeColor,
    string? IconUrl,
    int DisplayOrder,
    bool IsActive = true,
    decimal PriceUsd = 0,
    int MaxProductListings = 200);

public record AssignShopCategoryRequest(
    Guid? ShopCategoryId,
    string? CustomBadgeText = null);

public record RejectTierUpgradeRequest(
    string? Reason = null);

