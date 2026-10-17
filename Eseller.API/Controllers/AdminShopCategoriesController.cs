using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.ShopCategories.CreateShopCategory;
using Eseller.Application.Features.ShopCategories.DeleteShopCategory;
using Eseller.Application.Features.ShopCategories.GetShopCategories;
using Eseller.Application.Features.ShopCategories.UpdateShopCategory;
using Eseller.Domain.Entities.Catalog;
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
    private readonly ICurrentUserService _currentUserService;

    public AdminShopCategoriesController(
        ISender sender,
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _sender = sender;
        _context = context;
        _currentUserService = currentUserService;
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
    [Authorize(Roles = "SuperAdmin")]
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
            request.MaxProductListings,
            request.IsRecommended);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { categoryId = result.Value, message = "Shop category created successfully." });
    }

    /// <summary>
    /// Update an existing shop category/tier.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
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
            request.MaxProductListings,
            request.IsRecommended);

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
    [Authorize(Roles = "SuperAdmin")]
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
    [Authorize(Roles = "SuperAdmin,Partner")]
    public async Task<IActionResult> SeedStandardTiers(CancellationToken cancellationToken)
    {
        // Insert-only: never overwrite admin-edited tier prices/limits
        var specs = new[]
        {
            new { Name = "Bronze (Free)", Slug = "bronze-free", Price = 0m, Limit = 200, Badge = "Bronze (Free)", Color = "#94A3B8", Order = 1, Icon = "🥉", Recommended = false },
            new { Name = "Gold", Slug = "gold", Price = 1000m, Limit = 1000, Badge = "Gold", Color = "#F59E0B", Order = 2, Icon = "👑", Recommended = true },
            new { Name = "Diamond", Slug = "diamond", Price = 2000m, Limit = 5000, Badge = "Diamond", Color = "#06B6D4", Order = 3, Icon = "💎", Recommended = false }
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
                IconUrl = spec.Icon,
                DisplayOrder = spec.Order,
                PriceUsd = spec.Price,
                MaxProductListings = spec.Limit,
                IsRecommended = spec.Recommended,
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

        // Ensure exactly one recommended tier (default Gold) if none set
        var anyRecommended = await _context.ShopCategories
            .AnyAsync(c => c.DeletedAt == null && c.IsRecommended, cancellationToken);
        if (!anyRecommended)
        {
            var gold = await _context.ShopCategories
                .FirstOrDefaultAsync(c => c.DeletedAt == null && (c.Slug == "gold" || c.Name == "Gold"), cancellationToken);
            if (gold is not null)
                gold.IsRecommended = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            message = $"Standard tiers ready: Bronze Free ($0/200), Gold ($1000/1000), Diamond ($2000/5000). Created {createdCount} missing tier(s)."
        });
    }

    /// <summary>
    /// Get all tier upgrade requests (Pending + Approved + Rejected).
    /// Single projection query — no multi-query joins that can cancel under MARS.
    /// </summary>
    [HttpGet("tier-requests")]
    public async Task<IActionResult> GetTierRequests()
    {
        try
        {
            // Never bind to RequestAborted — admin list must always complete.
            var ct = CancellationToken.None;

            var query = _context.TierUpgradeRequests.AsNoTracking();
            var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, ct);
            if (partnerShopIds != null)
            {
                query = query.Where(r => partnerShopIds.Contains(r.ShopId));
            }

            var rows = await query
                .OrderByDescending(r => r.RequestedAt)
                .Select(req => new
                {
                    id = req.Id,
                    shopId = req.ShopId,
                    shopName = req.ShopName ?? "",
                    merchantName = req.MerchantName ?? "",
                    currentTier = req.CurrentTier ?? "Bronze (Free)",
                    requestedTier = req.RequestedTier ?? "",
                    requestedCategoryId = req.RequestedCategoryId,
                    badgeText = req.BadgeText ?? req.RequestedTier ?? "",
                    price = req.Price,
                    paymentMethod = req.PaymentMethod ?? "",
                    referenceNote = req.ReferenceNote ?? "",
                    receiptUrl = req.ReceiptUrl,
                    status = req.Status ?? "Pending",
                    requestedAt = req.RequestedAt,
                    reviewedAt = req.ReviewedAt,
                    reviewNotes = req.ReviewNotes
                })
                .ToListAsync(ct);

            // Capacity lookup (best-effort; never fail the list)
            var categories = await _context.ShopCategories.AsNoTracking()
                .Where(c => c.DeletedAt == null)
                .Select(c => new { c.Id, c.Name, c.BadgeText, c.Slug, c.MaxProductListings, c.PriceUsd })
                .ToListAsync(ct);

            var payload = rows.Select(req =>
            {
                var capacity = 0;
                try
                {
                    var key = (req.requestedTier ?? "").Trim();
                    var cat = categories.FirstOrDefault(c =>
                                   req.requestedCategoryId != null && c.Id == req.requestedCategoryId)
                           ?? categories.FirstOrDefault(c =>
                                   string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(c.BadgeText, key, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(c.Slug, key.Replace(' ', '-').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
                    if (cat == null && key.Contains("Platnium", StringComparison.OrdinalIgnoreCase))
                        cat = categories.FirstOrDefault(c =>
                            (c.Name ?? "").Contains("Plat", StringComparison.OrdinalIgnoreCase)
                            || (c.BadgeText ?? "").Contains("Plat", StringComparison.OrdinalIgnoreCase));
                    if (cat == null && key.Length > 0)
                        cat = categories.FirstOrDefault(c =>
                            (!string.IsNullOrWhiteSpace(c.Name) &&
                             (c.Name.Contains(key, StringComparison.OrdinalIgnoreCase)
                              || key.Contains(c.Name, StringComparison.OrdinalIgnoreCase)))
                            || (!string.IsNullOrWhiteSpace(c.BadgeText) &&
                                (c.BadgeText.Contains(key, StringComparison.OrdinalIgnoreCase)
                                 || key.Contains(c.BadgeText, StringComparison.OrdinalIgnoreCase))));
                    capacity = cat?.MaxProductListings ?? 0;
                }
                catch
                {
                    capacity = 0;
                }

                // For approved rows, prefer live badge as currentTier when blank
                var current = string.IsNullOrWhiteSpace(req.currentTier) ? (req.badgeText ?? req.requestedTier) : req.currentTier;

                return new
                {
                    req.id,
                    req.shopId,
                    req.shopName,
                    req.merchantName,
                    currentTier = current,
                    req.requestedTier,
                    req.requestedCategoryId,
                    req.badgeText,
                    req.price,
                    req.paymentMethod,
                    req.referenceNote,
                    req.receiptUrl,
                    req.status,
                    req.requestedAt,
                    req.reviewedAt,
                    req.reviewNotes,
                    maxProductListings = capacity
                };
            }).ToList();

            return Ok(payload);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Failed to load tier upgrade requests.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Approve tier upgrade request (updates shop tier in DB & notifies shopkeeper).
    /// </summary>
    [HttpPost("tier-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveTierRequest(
        Guid id,
        [FromServices] IAppNotificationService notifications,
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

        if (!await PartnerScopeHelper.IsShopPermittedForPartnerAsync(_context, _currentUserService, shop.Id, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden: You can only approve tier upgrade requests for shops registered through your referral link." });

        var categories = await _context.ShopCategories.Where(sc => sc.DeletedAt == null).ToListAsync(cancellationToken);
        var matchedCat = ResolveRequestedCategory(req, categories);
        if (matchedCat == null)
            return BadRequest(new { error = $"Could not resolve tier package '{req.RequestedTier}'. Create/activate that tier first, then approve." });

        ApplyTierToShop(shop, matchedCat);

        var approvedLabel = matchedCat.BadgeText ?? matchedCat.Name;
        req.Status = "Approved";
        req.ReviewedAt = DateTime.UtcNow;
        req.RequestedCategoryId = matchedCat.Id;
        req.BadgeText = approvedLabel;
        req.CurrentTier = approvedLabel; // admin "Current" column must reflect approved plan

        var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (shopkeeper != null)
        {
            await notifications.NotifyAsync(
                shopkeeper.AccountId,
                RoleType.Shopkeeper,
                NotificationType.TierBadgeRequestUpdated,
                "Tier Badge Request Updated",
                $"Congratulations! Your store is now on the {approvedLabel} plan.",
                NotificationEntityType.TierRequest,
                req.Id,
                "/dashboard",
                $"TierBadgeRequestUpdated:{req.Id:N}:Approved:Seller:{shopkeeper.AccountId:N}",
                cancellationToken);
        }

        return Ok(new
        {
            message = $"Tier upgrade for '{req.ShopName}' approved. Shop is now on {approvedLabel}.",
            currentTier = approvedLabel,
            shopCategoryId = matchedCat.Id,
            maxProductListings = matchedCat.MaxProductListings
        });
    }

    private static string ResolveShopTierLabel(Shop? shop, ShopCategory? cat)
    {
        if (shop == null) return "Bronze (Free)";
        if (!string.IsNullOrWhiteSpace(shop.CustomBadgeText)) return shop.CustomBadgeText!;
        if (!string.IsNullOrWhiteSpace(cat?.BadgeText)) return cat!.BadgeText!;
        if (!string.IsNullOrWhiteSpace(cat?.Name)) return cat!.Name;
        return "Bronze (Free)";
    }

    private static void ApplyTierToShop(Shop shop, ShopCategory tier)
    {
        shop.ShopCategoryId = tier.Id;
        shop.CustomBadgeText = tier.BadgeText ?? tier.Name;
        shop.SetUpdatedAt(DateTime.UtcNow);
    }

    private static ShopCategory? ResolveRequestedCategory(TierUpgradeRequest req, List<ShopCategory> categories)
    {
        if (req.RequestedCategoryId is Guid cid)
        {
            var byId = categories.FirstOrDefault(c => c.Id == cid);
            if (byId != null) return byId;
        }

        var key = (req.RequestedTier ?? string.Empty).Trim();
        var badgeKey = (req.BadgeText ?? string.Empty).Trim();

        // Exact name / badge / slug
        var exact = categories.FirstOrDefault(c =>
            (!string.IsNullOrWhiteSpace(key) && (
                string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.BadgeText, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Slug, key.Replace(' ', '-').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))) ||
            (!string.IsNullOrWhiteSpace(badgeKey) && (
                string.Equals(c.Name, badgeKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.BadgeText, badgeKey, StringComparison.OrdinalIgnoreCase))));
        if (exact != null) return exact;

        // Common typo: Platnium → Platinum / Diamond (capacity 5000 / high price)
        var normalized = key.Replace("Platnium", "Platinum", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(normalized, key, StringComparison.OrdinalIgnoreCase))
        {
            var byNorm = categories.FirstOrDefault(c =>
                string.Equals(c.Name, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.BadgeText, normalized, StringComparison.OrdinalIgnoreCase));
            if (byNorm != null) return byNorm;
        }

        // Fuzzy contains (prefer longest name match to avoid Bronze matching oddly)
        var fuzzy = categories
            .Where(c =>
                (!string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(key) &&
                    (c.Name.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                     key.Contains(c.Name, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(c.BadgeText) && !string.IsNullOrWhiteSpace(key) &&
                    (c.BadgeText.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                     key.Contains(c.BadgeText, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(c => (c.Name ?? string.Empty).Length)
            .FirstOrDefault();
        if (fuzzy != null) return fuzzy;

        // Match by listed price (e.g. $5000 custom package)
        if (req.Price > 0)
        {
            var byPrice = categories.FirstOrDefault(c => c.PriceUsd == req.Price);
            if (byPrice != null) return byPrice;
        }

        return null;
    }

    /// <summary>
    /// Reject tier upgrade request (updates DB & notifies shopkeeper).
    /// </summary>
    [HttpPost("tier-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectTierRequest(
        Guid id,
        [FromBody] RejectTierUpgradeRequest body,
        [FromServices] IAppNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var req = await _context.TierUpgradeRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (req == null) return NotFound(new { error = "Tier upgrade request not found." });

        if (req.ShopId != Guid.Empty && !await PartnerScopeHelper.IsShopPermittedForPartnerAsync(_context, _currentUserService, req.ShopId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden: You can only reject tier upgrade requests for shops registered through your referral link." });

        req.Status = "Rejected";
        req.ReviewNotes = body.Reason ?? "Payment verification could not be completed.";
        req.ReviewedAt = DateTime.UtcNow;

        Guid? sellerAccountId = null;
        if (req.ShopId != Guid.Empty)
        {
            var shop = await _context.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == req.ShopId, cancellationToken);
            if (shop != null)
            {
                var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);
                sellerAccountId = shopkeeper?.AccountId;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (sellerAccountId.HasValue)
        {
            await notifications.NotifyAsync(
                sellerAccountId.Value,
                RoleType.Shopkeeper,
                NotificationType.TierBadgeRequestUpdated,
                "Tier Badge Request Updated",
                $"Your package upgrade request for {req.RequestedTier} was rejected. Note: {req.ReviewNotes}",
                NotificationEntityType.TierRequest,
                req.Id,
                "/settings",
                $"TierBadgeRequestUpdated:{req.Id:N}:Rejected:Seller:{sellerAccountId.Value:N}",
                cancellationToken);
        }

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
    int MaxProductListings = 200,
    bool IsRecommended = false);

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
    int MaxProductListings = 200,
    bool IsRecommended = false);

public record AssignShopCategoryRequest(
    Guid? ShopCategoryId,
    string? CustomBadgeText = null);

public record RejectTierUpgradeRequest(
    string? Reason = null);

