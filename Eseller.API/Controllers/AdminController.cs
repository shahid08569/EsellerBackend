using Eseller.Application.Abstractions;
using Eseller.Application.Features.Admin.Shopkeepers.ApproveShopkeeper;
using Eseller.Application.Features.Admin.Shopkeepers.RejectShopkeeper;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;

    public AdminController(ISender sender, IApplicationDbContext context)
    {
        _sender = sender;
        _context = context;
    }

    /// <summary>
    /// Get all shopkeepers, optionally filtered by status, enriched with login & shop stats.
    /// </summary>
    [HttpGet("shopkeepers")]
    public async Task<IActionResult> GetShopkeepers(
        [FromQuery] ShopkeeperApprovalStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _context.Shopkeepers.AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        var shopkeepers = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (!shopkeepers.Any())
            return Ok(Array.Empty<object>());

        var accountIds = shopkeepers.Select(s => s.AccountId).ToList();
        var shopkeeperIds = shopkeepers.Select(s => s.Id).ToList();

        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Include inactive (pending) shops; exclude soft-deleted so deleted stores leave the list
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => shopkeeperIds.Contains(s.ShopkeeperId) && s.DeletedAt == null)
            .ToDictionaryAsync(s => s.ShopkeeperId, cancellationToken);

        var shopIds = shops.Values.Select(sh => sh.Id).ToList();

        // Real product counts per shop
        var realProductCountsByShop = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => shopIds.Contains(p.ShopId) && p.DeletedAt == null)
            .GroupBy(p => p.ShopId)
            .Select(g => new { ShopId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ShopId, x => x.Count, cancellationToken);

        // Real average ratings per shop from approved reviews
        var shopProductMap = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => shopIds.Contains(p.ShopId) && p.DeletedAt == null)
            .Select(p => new { p.Id, p.ShopId })
            .ToListAsync(cancellationToken);

        var prodToShop = shopProductMap.ToDictionary(p => p.Id, p => p.ShopId);
        var prodIds = prodToShop.Keys.ToList();

        var shopReviews = await _context.Reviews
            .Where(r => prodIds.Contains(r.ProductId) && r.IsApproved)
            .Select(r => new { r.ProductId, r.Rating })
            .ToListAsync(cancellationToken);

        var ratingByShop = shopReviews
            .GroupBy(r => prodToShop[r.ProductId])
            .ToDictionary(g => g.Key, g => Math.Round((decimal)g.Average(r => r.Rating), 1));

        var sellerOrders = await _context.SellerOrders
            .Where(so => shopIds.Contains(so.ShopId))
            .Select(so => new { so.Id, so.ShopId })
            .ToListAsync(cancellationToken);

        var sellerOrderIds = sellerOrders.Select(so => so.Id).ToList();
        var orderItems = await _context.OrderRequestItems
            .Where(ori => sellerOrderIds.Contains(ori.SellerOrderId))
            .Select(ori => new { ori.SellerOrderId, ori.TotalPriceSnapshot })
            .ToListAsync(cancellationToken);

        var itemPricesBySellerOrder = orderItems.GroupBy(ori => ori.SellerOrderId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalPriceSnapshot));
        var ordersCountByShop = sellerOrders.GroupBy(so => so.ShopId).ToDictionary(g => g.Key, g => g.Count());
        var revenueByShop = sellerOrders.GroupBy(so => so.ShopId).ToDictionary(g => g.Key, g => g.Sum(so => itemPricesBySellerOrder.TryGetValue(so.Id, out var p) ? p : 0m));

        var locationLogs = await _context.LocationLogs
            .Where(l => l.AccountId.HasValue && accountIds.Contains(l.AccountId.Value))
            .OrderByDescending(l => l.RecordedAt)
            .ToListAsync(cancellationToken);

        var latestLogByAccount = locationLogs
            .GroupBy(l => l.AccountId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = shopkeepers.Select(s =>
        {
            var acc = accounts.GetValueOrDefault(s.AccountId);
            var shop = shops.GetValueOrDefault(s.Id);
            latestLogByAccount.TryGetValue(s.AccountId, out var loc);
            var shopId = shop?.Id;
            var totOrders = shopId.HasValue && ordersCountByShop.TryGetValue(shopId.Value, out var oCnt) ? oCnt : 0;
            var totRev = shopId.HasValue && revenueByShop.TryGetValue(shopId.Value, out var rev) ? rev : 0m;
            var prodCnt = shopId.HasValue && realProductCountsByShop.TryGetValue(shopId.Value, out var pCnt) ? pCnt : (shop?.TotalProducts ?? 0);
            var shopRating = shopId.HasValue && ratingByShop.TryGetValue(shopId.Value, out var rVal) ? rVal : 5.0m;

            return new
            {
                id = s.Id,
                accountId = s.AccountId,
                shopId = shop?.Id,
                name = s.Name,
                email = acc?.Email ?? string.Empty,
                phone = s.Phone,
                documentType = s.DocumentType ?? "CNIC / National ID",
                documentNumber = s.DocumentNumber,
                cnicFrontUrl = s.CnicFrontUrl,
                cnicBackUrl = s.CnicBackUrl,
                documentUrl = s.DocumentUrl,
                storeName = shop?.Name ?? string.Empty,
                storeUrl = shop?.Slug ?? string.Empty,
                logoUrl = shop?.LogoUrl,
                bannerUrl = shop?.BannerUrl,
                city = shop?.City ?? string.Empty,
                country = shop?.Country ?? string.Empty,
                status = s.Status.ToString(),
                approvedAt = s.ApprovedAt,
                rejectionReason = s.RejectionReason,
                createdAt = s.CreatedAt,
                isActive = acc?.IsActive ?? true,
                lastLoginAt = acc?.LastLoginAt,
                lastLoginCity = loc?.City,
                lastLoginCountry = loc?.Country,
                lastLoginDevice = loc?.DeviceType,
                latitude = loc?.Latitude,
                longitude = loc?.Longitude,
                totalProducts = prodCnt,
                totalOrders = totOrders,
                totalRevenue = totRev,
                rating = shopRating
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Alias route for sellers
    /// </summary>
    [HttpGet("sellers")]
    public Task<IActionResult> GetSellers(
        [FromQuery] ShopkeeperApprovalStatus? status,
        CancellationToken cancellationToken)
    {
        return GetShopkeepers(status, cancellationToken);
    }

    /// <summary>
    /// Get login and location activity for a specific shopkeeper/seller
    /// </summary>
    [HttpGet("shopkeepers/{id:guid}/logins")]
    public async Task<IActionResult> GetShopkeeperLogins(
        Guid id,
        CancellationToken cancellationToken)
    {
        var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (shopkeeper == null) return NotFound(new { error = "Shopkeeper not found." });

        var logs = await _context.LocationLogs
            .Where(l => l.AccountId == shopkeeper.AccountId)
            .OrderByDescending(l => l.RecordedAt)
            .Take(100)
            .Select(l => new
            {
                l.Id,
                l.City,
                l.Country,
                l.CountryCode,
                l.DeviceType,
                l.IpAddress,
                l.Latitude,
                l.Longitude,
                l.Action,
                l.RecordedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }

    [HttpGet("sellers/{id:guid}/logins")]
    public Task<IActionResult> GetSellerLogins(
        Guid id,
        CancellationToken cancellationToken)
    {
        return GetShopkeeperLogins(id, cancellationToken);
    }

    /// <summary>
    /// Toggle seller active/suspended status
    /// </summary>
    [HttpPut("shopkeepers/{id:guid}/status")]
    public async Task<IActionResult> SetShopkeeperStatus(
        Guid id,
        [FromBody] SetShopkeeperStatusRequest request,
        CancellationToken cancellationToken)
    {
        var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (shopkeeper == null) return NotFound(new { error = "Shopkeeper not found." });

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == shopkeeper.AccountId, cancellationToken);
        if (account != null)
        {
            account.IsActive = request.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = $"Seller status updated to {(request.IsActive ? "Active" : "Suspended")}." });
    }

    /// <summary>
    /// Approve a pending shopkeeper application.
    /// </summary>
    [HttpPost("shopkeepers/{id:guid}/approve")]
    public async Task<IActionResult> ApproveShopkeeper(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ApproveShopkeeperCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Shopkeeper approved successfully." });
    }

    /// <summary>
    /// Reject a pending shopkeeper application with a reason.
    /// </summary>
    [HttpPost("shopkeepers/{id:guid}/reject")]
    public async Task<IActionResult> RejectShopkeeper(
        Guid id,
        [FromBody] RejectShopkeeperRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectShopkeeperCommand(id, request.Reason);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Shopkeeper rejected successfully." });
    }

    /// <summary>
    /// Delete / deactivate shopkeeper and their store
    /// </summary>
    [HttpDelete("shopkeepers/{id:guid}")]
    public async Task<IActionResult> DeleteShopkeeper(
        Guid id,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(s => s.Id == id || s.AccountId == id, cancellationToken);
        Shop? shop = null;
        if (shopkeeper == null)
        {
            shop = await _context.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (shop != null)
            {
                shopkeeper = await _context.Shopkeepers.FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);
            }
        }
        else
        {
            shop = await _context.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id, cancellationToken);
        }

        if (shopkeeper == null && shop == null) return NotFound(new { error = "Shop or seller profile not found." });

        if (shopkeeper != null)
        {
            shopkeeper.Status = ShopkeeperApprovalStatus.Rejected;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                shopkeeper.RejectionReason = reason;
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == shopkeeper.AccountId, cancellationToken);
            if (account != null)
            {
                account.IsActive = false;
            }
        }

        if (shop != null)
        {
            shop.IsActive = false;
            shop.DeletedAt = DateTime.UtcNow;
            shop.SetUpdatedAt(DateTime.UtcNow);

            var prods = await _context.Products.IgnoreQueryFilters()
                .Where(p => p.ShopId == shop.Id && p.DeletedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var p in prods)
            {
                p.DeletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Seller and store removed successfully." });
    }

    [HttpDelete("sellers/{id:guid}")]
    public Task<IActionResult> DeleteSeller(
        Guid id,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        return DeleteShopkeeper(id, reason, cancellationToken);
    }

    /// <summary>
    /// Get all affiliate users with referral stats, commissions, and balances.
    /// </summary>
    [HttpGet("affiliates")]
    public async Task<IActionResult> GetAffiliates(CancellationToken cancellationToken)
    {
        var affiliates = await _context.Affiliates
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var accountIds = affiliates.Select(a => a.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(acc => accountIds.Contains(acc.Id))
            .ToDictionaryAsync(acc => acc.Id, cancellationToken);

        var affiliateIds = affiliates.Select(a => a.Id).ToList();
        var commissions = await _context.AffiliateCommissions
            .Where(c => affiliateIds.Contains(c.AffiliateId))
            .ToListAsync(cancellationToken);

        var list = affiliates.Select(a =>
        {
            accounts.TryGetValue(a.AccountId, out var acc);
            var aComms = commissions.Where(c => c.AffiliateId == a.Id).ToList();
            var totalEarnings = aComms
                .Where(c => c.Status == AffiliateCommissionStatus.Approved || c.Status == AffiliateCommissionStatus.Paid)
                .Sum(c => c.CommissionAmount);

            if (totalEarnings == 0 && a.TotalEarned > 0)
            {
                totalEarnings = a.TotalEarned;
            }

            var pendingEarnings = aComms
                .Where(c => c.Status == AffiliateCommissionStatus.Pending)
                .Sum(c => c.CommissionAmount);

            var fullName = !string.IsNullOrWhiteSpace(acc?.Username) ? acc.Username : (acc?.Email ?? "Affiliate User");

            return new
            {
                id = a.Id,
                accountId = a.AccountId,
                name = fullName,
                email = acc?.Email ?? string.Empty,
                referralCode = a.ReferralCode,
                commissionRate = a.CommissionRate,
                totalClicks = a.TotalClicks,
                totalConversions = a.TotalConversions,
                totalEarnings = totalEarnings,
                totalEarned = totalEarnings,
                unpaidBalance = pendingEarnings,
                isActive = a.IsActive,
                createdAt = a.CreatedAt
            };
        }).ToList();

        return Ok(list);
    }

    /// <summary>
    /// SuperAdmin: set platform rating for any shop (0–5).
    /// </summary>
    [HttpPut("shops/{id:guid}/rating")]
    public async Task<IActionResult> SetShopRating(
        Guid id,
        [FromBody] SetShopRatingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rating < 0 || request.Rating > 5)
            return BadRequest(new { error = "Rating must be between 0 and 5.", errorCode = "INVALID_RATING" });

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return NotFound(new { error = "Shop not found.", errorCode = "SHOP_NOT_FOUND" });

        shop.Rating = Math.Round(request.Rating, 1);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Shop rating updated.",
            shopId = shop.Id,
            rating = shop.Rating
        });
    }
}

public record RejectShopkeeperRequest(string Reason);
public record SetShopkeeperStatusRequest(bool IsActive);
public record SetShopRatingRequest(decimal Rating);