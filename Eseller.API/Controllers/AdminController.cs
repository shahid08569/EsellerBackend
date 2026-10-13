using Eseller.Application.Abstractions;
using Eseller.Application.Features.Shops;
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
        try
        {
            var query = _context.Shopkeepers.AsNoTracking().AsQueryable();

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
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            // Include inactive (pending) shops; exclude soft-deleted so deleted stores leave the list
            var shops = await _context.Shops
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => shopkeeperIds.Contains(s.ShopkeeperId) && s.DeletedAt == null)
                .GroupBy(s => s.ShopkeeperId)
                .Select(g => g.OrderByDescending(s => s.CreatedAt).First())
                .ToDictionaryAsync(s => s.ShopkeeperId, cancellationToken);

            var shopIds = shops.Values.Select(sh => sh.Id).ToList();

            // Real product counts per shop
            var realProductCountsByShop = shopIds.Count == 0
                ? new Dictionary<Guid, int>()
                : await _context.Products
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(p => shopIds.Contains(p.ShopId) && p.DeletedAt == null)
                    .GroupBy(p => p.ShopId)
                    .Select(g => new { ShopId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ShopId, x => x.Count, cancellationToken);

            // Real average ratings per shop from approved reviews
            var shopProductMap = shopIds.Count == 0
                ? new List<(Guid Id, Guid ShopId)>()
                : (await _context.Products
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(p => shopIds.Contains(p.ShopId) && p.DeletedAt == null)
                    .Select(p => new { p.Id, p.ShopId })
                    .ToListAsync(cancellationToken))
                  .Select(p => (p.Id, p.ShopId))
                  .ToList();

            var prodToShop = shopProductMap.ToDictionary(p => p.Id, p => p.ShopId);
            var prodIds = prodToShop.Keys.ToList();

            var shopReviews = prodIds.Count == 0
                ? new List<(Guid ProductId, int Rating)>()
                : (await _context.Reviews
                    .AsNoTracking()
                    .Where(r => prodIds.Contains(r.ProductId) && r.IsApproved)
                    .Select(r => new { r.ProductId, r.Rating })
                    .ToListAsync(cancellationToken))
                  .Select(r => (r.ProductId, r.Rating))
                  .ToList();

            var ratingByShop = shopReviews
                .GroupBy(r => prodToShop[r.ProductId])
                .ToDictionary(g => g.Key, g => Math.Round((decimal)g.Average(r => r.Rating), 1));

            var sellerOrders = shopIds.Count == 0
                ? new List<(Guid Id, Guid ShopId)>()
                : (await _context.SellerOrders
                    .AsNoTracking()
                    .Where(so => shopIds.Contains(so.ShopId))
                    .Select(so => new { so.Id, so.ShopId })
                    .ToListAsync(cancellationToken))
                  .Select(so => (so.Id, so.ShopId))
                  .ToList();

            var sellerOrderIds = sellerOrders.Select(so => so.Id).ToList();
            var orderItems = sellerOrderIds.Count == 0
                ? new List<(Guid SellerOrderId, decimal TotalPriceSnapshot)>()
                : (await _context.OrderRequestItems
                    .AsNoTracking()
                    .Where(ori => sellerOrderIds.Contains(ori.SellerOrderId))
                    .Select(ori => new { ori.SellerOrderId, ori.TotalPriceSnapshot })
                    .ToListAsync(cancellationToken))
                  .Select(ori => (ori.SellerOrderId, ori.TotalPriceSnapshot))
                  .ToList();

            var itemPricesBySellerOrder = orderItems.GroupBy(ori => ori.SellerOrderId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalPriceSnapshot));
            var ordersCountByShop = sellerOrders.GroupBy(so => so.ShopId).ToDictionary(g => g.Key, g => g.Count());
            var revenueByShop = sellerOrders.GroupBy(so => so.ShopId).ToDictionary(g => g.Key, g => g.Sum(so => itemPricesBySellerOrder.TryGetValue(so.Id, out var p) ? p : 0m));

            var locationLogs = accountIds.Count == 0
                ? new List<Domain.Entities.Identity.LocationLog>()
                : await _context.LocationLogs
                    .AsNoTracking()
                    .Where(l => l.AccountId.HasValue && accountIds.Contains(l.AccountId.Value))
                    .OrderByDescending(l => l.RecordedAt)
                    .ToListAsync(cancellationToken);

            var latestLogByAccount = locationLogs
                .GroupBy(l => l.AccountId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // Referrals lookup
            var affiliates = await _context.Affiliates
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var affiliateByAccount = affiliates.ToDictionary(a => a.AccountId);
            var affiliateById = affiliates.ToDictionary(a => a.Id);

            var referralClicks = await _context.AffiliateClicks
                .AsNoTracking()
                .Where(c => c.Converted)
                .ToListAsync(cancellationToken);

            static Guid? ExtractShopId(Eseller.Domain.Entities.Affiliate.AffiliateClick c)
            {
                if (!string.IsNullOrWhiteSpace(c.IpAddress) && Guid.TryParse(c.IpAddress.Trim(), out var idFromIp))
                    return idFromIp;
                if (!string.IsNullOrWhiteSpace(c.UserAgent))
                {
                    var parts = c.UserAgent.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && Guid.TryParse(parts[1].Trim(), out var idFromUa))
                        return idFromUa;
                }
                return c.ProductId;
            }

            var referralCountByAffiliate = referralClicks
                .GroupBy(c => c.AffiliateId)
                .ToDictionary(g => g.Key, g => g.Count());

            var referredByShop = referralClicks
                .Select(c => new { Click = c, ShopId = ExtractShopId(c) })
                .Where(x => x.ShopId.HasValue)
                .GroupBy(x => x.ShopId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Click);

            var result = shopkeepers.Select(s =>
            {
                var acc = accounts.GetValueOrDefault(s.AccountId);
                var shop = shops.GetValueOrDefault(s.Id);
                latestLogByAccount.TryGetValue(s.AccountId, out var loc);
                var shopId = shop?.Id;
                var totOrders = shopId.HasValue && ordersCountByShop.TryGetValue(shopId.Value, out var oCnt) ? oCnt : 0;
                var totRev = shopId.HasValue && revenueByShop.TryGetValue(shopId.Value, out var rev) ? rev : 0m;
                var prodCnt = shopId.HasValue && realProductCountsByShop.TryGetValue(shopId.Value, out var pCnt) ? pCnt : (shop?.TotalProducts ?? 0);

                affiliateByAccount.TryGetValue(s.AccountId, out var myAffiliate);
                var myRefCode = myAffiliate?.ReferralCode;
                var myRefCount = myAffiliate != null && referralCountByAffiliate.TryGetValue(myAffiliate.Id, out var rCnt) ? rCnt : 0;

                string? refByCode = null;
                string? refByName = null;
                if (shopId.HasValue && referredByShop.TryGetValue(shopId.Value, out var whoReferredMe))
                {
                    if (affiliateById.TryGetValue(whoReferredMe.AffiliateId, out var parentAffiliate))
                    {
                        refByCode = parentAffiliate.ReferralCode;
                        accounts.TryGetValue(parentAffiliate.AccountId, out var parentAcc);
                        refByName = parentAcc?.Username ?? "Affiliate";
                    }
                }

                // Platform rating = SuperAdmin-set Shop.Rating (authoritative).
                // Fallback to review average only when admin has not set a rating yet.
                decimal shopRating = 0m;
                if (shop is not null && shop.Rating > 0)
                    shopRating = shop.Rating;
                else if (shopId.HasValue && ratingByShop.TryGetValue(shopId.Value, out var rVal))
                    shopRating = rVal;

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
                    rating = shopRating,
                    referralCode = myRefCode,
                    referralsCount = myRefCount,
                    referredByCode = refByCode,
                    referredByName = refByName
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Client navigated away / refresh aborted the request — not a fault
            return NoContent();
        }
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
    /// Legacy endpoint for affiliate commissions. Replaced by AdminReferralsController.
    /// </summary>
    [HttpGet("affiliates-legacy")]
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

    // ═══════════════════════════════════════════════════════════
    // SHOP VISITOR & TRAFFIC ANALYTICS (SUPERADMIN)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// SuperAdmin: list all shops with visitor traffic metrics.
    /// </summary>
    [HttpGet("shops/traffic")]
    public async Task<IActionResult> GetAllShopsTraffic(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var firstOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthUtc = firstOfMonthUtc.AddMonths(1);
        var firstOfLastMonthUtc = firstOfMonthUtc.AddMonths(-1);

        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var shopIds = shops.Select(s => s.Id).ToList();

        var shopkeeperIds = shops.Select(s => s.ShopkeeperId).Distinct().ToList();
        var shopkeepers = await _context.Shopkeepers
            .Where(sk => shopkeeperIds.Contains(sk.Id))
            .ToDictionaryAsync(sk => sk.Id, cancellationToken);

        var accountIds = shopkeepers.Values.Select(sk => sk.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Group visits by shop
        var allTimeTotals = await _context.ShopTraffics
            .Where(t => shopIds.Contains(t.ShopId))
            .GroupBy(t => t.ShopId)
            .Select(g => new { ShopId = g.Key, Total = g.Sum(x => (long)x.VisitCount) })
            .ToDictionaryAsync(x => x.ShopId, x => x.Total, cancellationToken);

        var todayTotals = await _context.ShopTraffics
            .Where(t => shopIds.Contains(t.ShopId) && t.Date == todayUtc)
            .GroupBy(t => t.ShopId)
            .Select(g => new { ShopId = g.Key, Total = g.Sum(x => x.VisitCount) })
            .ToDictionaryAsync(x => x.ShopId, x => x.Total, cancellationToken);

        var thisMonthTotals = await _context.ShopTraffics
            .Where(t => shopIds.Contains(t.ShopId) && t.Date >= firstOfMonthUtc && t.Date < nextMonthUtc)
            .GroupBy(t => t.ShopId)
            .Select(g => new { ShopId = g.Key, Total = g.Sum(x => (long)x.VisitCount) })
            .ToDictionaryAsync(x => x.ShopId, x => x.Total, cancellationToken);

        var lastMonthTotals = await _context.ShopTraffics
            .Where(t => shopIds.Contains(t.ShopId) && t.Date >= firstOfLastMonthUtc && t.Date < firstOfMonthUtc)
            .GroupBy(t => t.ShopId)
            .Select(g => new { ShopId = g.Key, Total = g.Sum(x => (long)x.VisitCount) })
            .ToDictionaryAsync(x => x.ShopId, x => x.Total, cancellationToken);

        var items = shops.Select(s =>
        {
            var sk = shopkeepers.GetValueOrDefault(s.ShopkeeperId);
            var acc = sk != null ? accounts.GetValueOrDefault(sk.AccountId) : null;

            var total = allTimeTotals.GetValueOrDefault(s.Id, 0L);
            var today = todayTotals.GetValueOrDefault(s.Id, 0);
            var thisMonth = thisMonthTotals.GetValueOrDefault(s.Id, 0L);
            var lastMonth = lastMonthTotals.GetValueOrDefault(s.Id, 0L);

            decimal growth = 0m;
            if (lastMonth > 0)
                growth = Math.Round(((decimal)thisMonth - lastMonth) / lastMonth * 100m, 1);
            else if (thisMonth > 0)
                growth = 100m;

            return new AdminShopTrafficItemDto
            {
                ShopId = s.Id,
                ShopName = s.Name,
                Slug = s.Slug,
                LogoUrl = s.LogoUrl,
                OwnerName = sk?.Name ?? "Merchant",
                OwnerEmail = acc?.Email ?? string.Empty,
                Rating = s.Rating,
                IsApproved = sk?.Status == ShopkeeperApprovalStatus.Approved,
                IsActive = s.IsActive,
                TotalVisits = total,
                TodayVisits = today,
                ThisMonthVisits = thisMonth,
                LastMonthVisits = lastMonth,
                GrowthRate = growth,
                TrendDirection = growth > 0 ? "up" : (growth < 0 ? "down" : "flat")
            };
        }).ToList();

        return Ok(items);
    }

    /// <summary>
    /// SuperAdmin: get detailed traffic summary & timeline for a specific shop.
    /// </summary>
    [HttpGet("shops/{id:guid}/traffic")]
    public async Task<IActionResult> GetShopTrafficDetail(
        Guid id,
        [FromQuery] string range = "30d",
        CancellationToken cancellationToken = default)
    {
        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return NotFound(new { error = "Shop not found.", errorCode = "SHOP_NOT_FOUND" });

        var summary = await ShopTrafficHelper.BuildSummaryAsync(shop.Id, shop.Name, range, _context, cancellationToken, shop.LogoUrl);
        return Ok(summary);
    }

    /// <summary>
    /// SuperAdmin: manually set / override total visitors for a specific month.
    /// </summary>
    [HttpPost("shops/{id:guid}/traffic/monthly")]
    public async Task<IActionResult> OverrideMonthlyTraffic(
        Guid id,
        [FromBody] OverrideMonthlyTrafficRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Year < 2020 || request.Year > 2100 || request.Month < 1 || request.Month > 12)
            return BadRequest(new { error = "Invalid year or month.", errorCode = "INVALID_MONTH" });

        if (request.TotalVisits < 0)
            return BadRequest(new { error = "Visits count cannot be negative.", errorCode = "INVALID_VISITS" });

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return NotFound(new { error = "Shop not found.", errorCode = "SHOP_NOT_FOUND" });

        var monthAnchor = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthAnchor.AddMonths(1);

        // Remove any existing records for this month to cleanly match target
        var monthRecords = await _context.ShopTraffics
            .Where(t => t.ShopId == id && t.Date >= monthAnchor && t.Date < nextMonth)
            .ToListAsync(cancellationToken);

        if (monthRecords.Any())
        {
            _context.ShopTraffics.RemoveRange(monthRecords);
        }

        var unique = request.UniqueVisitors.HasValue && request.UniqueVisitors.Value > 0
            ? request.UniqueVisitors.Value
            : (int)Math.Round(request.TotalVisits * 0.75);

        var newRecord = new ShopTraffic
        {
            ShopId = id,
            Date = monthAnchor,
            VisitCount = request.TotalVisits,
            UniqueVisitors = unique,
            IsManual = true,
            Note = !string.IsNullOrWhiteSpace(request.Note)
                ? request.Note
                : $"Manual override by Admin ({monthAnchor:MMMM yyyy})"
        };
        newRecord.SetCreatedAt(DateTime.UtcNow);
        _context.ShopTraffics.Add(newRecord);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = $"Successfully updated visitor count for {monthAnchor:MMMM yyyy} to {request.TotalVisits:N0}.",
            shopId = id,
            year = request.Year,
            month = request.Month,
            totalVisits = request.TotalVisits
        });
    }

    /// <summary>
    /// SuperAdmin: manually set / override visitors for a specific date.
    /// </summary>
    [HttpPost("shops/{id:guid}/traffic/daily")]
    public async Task<IActionResult> OverrideDailyTraffic(
        Guid id,
        [FromBody] OverrideDailyTrafficRequest request,
        CancellationToken cancellationToken)
    {
        if (request.VisitCount < 0)
            return BadRequest(new { error = "Visit count cannot be negative.", errorCode = "INVALID_COUNT" });

        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return NotFound(new { error = "Shop not found.", errorCode = "SHOP_NOT_FOUND" });

        var targetDate = request.Date.Date;
        var record = await _context.ShopTraffics
            .FirstOrDefaultAsync(t => t.ShopId == id && t.Date == targetDate, cancellationToken);

        var unique = request.UniqueVisitors.HasValue && request.UniqueVisitors.Value > 0
            ? request.UniqueVisitors.Value
            : (int)Math.Round(request.VisitCount * 0.75);

        if (record == null)
        {
            record = new ShopTraffic
            {
                ShopId = id,
                Date = targetDate,
                VisitCount = request.VisitCount,
                UniqueVisitors = unique,
                IsManual = true,
                Note = request.Note ?? "Manual daily override by Admin"
            };
            record.SetCreatedAt(DateTime.UtcNow);
            _context.ShopTraffics.Add(record);
        }
        else
        {
            record.VisitCount = request.VisitCount;
            record.UniqueVisitors = unique;
            record.IsManual = true;
            if (!string.IsNullOrWhiteSpace(request.Note)) record.Note = request.Note;
            record.SetUpdatedAt(DateTime.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = $"Successfully updated visitor count for {targetDate:yyyy-MM-dd} to {request.VisitCount:N0}.",
            shopId = id,
            date = targetDate.ToString("yyyy-MM-dd"),
            visitCount = request.VisitCount
        });
    }
}

public record RejectShopkeeperRequest(string Reason);
public record SetShopkeeperStatusRequest(bool IsActive);
public record SetShopRatingRequest(decimal Rating);
public record OverrideMonthlyTrafficRequest(
    int Year,
    int Month,
    int TotalVisits,
    int? UniqueVisitors = null,
    string? Note = null);
public record OverrideDailyTrafficRequest(
    DateTime Date,
    int VisitCount,
    int? UniqueVisitors = null,
    string? Note = null);