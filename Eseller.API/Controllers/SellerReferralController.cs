using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Affiliate;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/seller/referral")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class SellerReferralController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public SellerReferralController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    /// <summary>
    /// Get or generate this seller's unique invitation referral code and referred store metrics.
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyReferral(CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized(new { error = "User is not authenticated.", errorCode = "UNAUTHENTICATED" });

        var accountId = _currentUserService.AccountId.Value;

        // Find existing affiliate or generate one
        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

        var now = _dateTime.UtcNow;
        if (affiliate is null)
        {
            // Generate a clean, random, unique referral code e.g. REF-7K9M2X
            string uniqueCode;
            int attempts = 0;
            do
            {
                var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                uniqueCode = $"REF-{suffix}";
                attempts++;
            } while (await _context.Affiliates.AnyAsync(a => a.ReferralCode == uniqueCode, cancellationToken) && attempts < 10);

            affiliate = new Affiliate
            {
                AccountId = accountId,
                ReferralCode = uniqueCode,
                TotalClicks = 0,
                TotalConversions = 0,
                TotalEarned = 0,
                IsActive = true
            };
            affiliate.SetCreatedAt(now);
            affiliate.SetUpdatedAt(now);

            _context.Affiliates.Add(affiliate);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Query all shops referred by this affiliate
        var conversions = await _context.AffiliateClicks
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id && c.Converted)
            .OrderByDescending(c => c.ClickedAt)
            .ToListAsync(cancellationToken);

        static Guid? ParseShopId(AffiliateClick c)
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

        var shopIds = conversions
            .Select(ParseShopId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var referredShopsList = new List<object>();

        if (shopIds.Any())
        {
            var shops = await _context.Shops
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => shopIds.Contains(s.Id) && s.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var shopkeeperIds = shops.Select(s => s.ShopkeeperId).Distinct().ToList();

            var shopkeepers = await _context.Shopkeepers
                .AsNoTracking()
                .Where(sk => shopkeeperIds.Contains(sk.Id))
                .ToDictionaryAsync(sk => sk.Id, cancellationToken);

            var accountIds = shopkeepers.Values.Select(sk => sk.AccountId).Distinct().ToList();

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            var productCounts = await _context.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => shopIds.Contains(p.ShopId) && p.DeletedAt == null)
                .GroupBy(p => p.ShopId)
                .Select(g => new { ShopId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ShopId, g => g.Count, cancellationToken);

            foreach (var conv in conversions)
            {
                var sId = ParseShopId(conv);
                if (!sId.HasValue) continue;
                var shop = shops.FirstOrDefault(s => s.Id == sId.Value);
                if (shop is null) continue;

                shopkeepers.TryGetValue(shop.ShopkeeperId, out var sk);
                Account? acc = null;
                if (sk != null) accounts.TryGetValue(sk.AccountId, out acc);

                var prodCount = productCounts.TryGetValue(shop.Id, out var pc) ? pc : shop.TotalProducts;
                var isApproved = sk?.Status == ShopkeeperApprovalStatus.Approved;

                referredShopsList.Add(new
                {
                    shopId = shop.Id,
                    shopName = shop.Name,
                    shopSlug = shop.Slug,
                    sellerName = sk?.Name ?? acc?.Username ?? "Merchant",
                    email = acc?.Email ?? "",
                    phone = sk?.Phone ?? shop.Phone ?? "",
                    status = sk?.Status.ToString() ?? "Pending",
                    isApproved,
                    totalProducts = prodCount,
                    hasListedProducts = prodCount > 0,
                    registeredAt = conv.ClickedAt
                });
            }
        }

        var activeReferredCount = referredShopsList.Count(s =>
        {
            var prop = s.GetType().GetProperty("hasListedProducts");
            return prop != null && (bool)prop.GetValue(s)!;
        });

        return Ok(new
        {
            referralCode = affiliate.ReferralCode,
            totalClicks = affiliate.TotalClicks,
            totalReferredStores = referredShopsList.Count,
            activeReferredStores = activeReferredCount,
            totalEarned = affiliate.TotalEarned,
            referredStores = referredShopsList
        });
    }
}
