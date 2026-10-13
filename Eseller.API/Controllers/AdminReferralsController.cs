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
[Route("api/v1/admin/affiliates")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminReferralsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminReferralsController(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all seller affiliates / referrers with conversion metrics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAffiliates(CancellationToken cancellationToken)
    {
        // Ensure all shopkeepers have an affiliate referral record
        var existingAccountIds = await _context.Affiliates
            .Select(a => a.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missingShopkeepers = await _context.Shopkeepers
            .Where(sk => !existingAccountIds.Contains(sk.AccountId))
            .ToListAsync(cancellationToken);

        if (missingShopkeepers.Any())
        {
            foreach (var sk in missingShopkeepers)
            {
                var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                var uniqueCode = $"REF-{suffix}";
                var aff = new Affiliate
                {
                    AccountId = sk.AccountId,
                    ReferralCode = uniqueCode,
                    TotalClicks = 0,
                    TotalConversions = 0,
                    TotalEarned = 0,
                    IsActive = true
                };
                aff.SetCreatedAt(DateTime.UtcNow);
                aff.SetUpdatedAt(DateTime.UtcNow);
                _context.Affiliates.Add(aff);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        var affiliates = await _context.Affiliates
            .AsNoTracking()
            .OrderByDescending(a => a.TotalConversions)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        if (!affiliates.Any())
            return Ok(Array.Empty<object>());

        var accountIds = affiliates.Select(a => a.AccountId).Distinct().ToList();

        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var shopkeepers = await _context.Shopkeepers
            .AsNoTracking()
            .Where(sk => accountIds.Contains(sk.AccountId))
            .ToDictionaryAsync(sk => sk.AccountId, cancellationToken);

        var skIds = shopkeepers.Values.Select(sk => sk.Id).ToList();
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => skIds.Contains(s.ShopkeeperId) && s.DeletedAt == null)
            .ToDictionaryAsync(s => s.ShopkeeperId, cancellationToken);

        var affiliateIds = affiliates.Select(a => a.Id).ToList();

        // Count conversions and active conversions
        var conversions = await _context.AffiliateClicks
            .AsNoTracking()
            .Where(c => affiliateIds.Contains(c.AffiliateId) && c.Converted)
            .ToListAsync(cancellationToken);

        var referredShopIds = conversions
            .Select(ParseShopId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var activeProductShopIds = new HashSet<Guid>();
        if (referredShopIds.Any())
        {
            activeProductShopIds = (await _context.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => referredShopIds.Contains(p.ShopId) && p.DeletedAt == null)
                .Select(p => p.ShopId)
                .Distinct()
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        var result = affiliates.Select(a =>
        {
            accounts.TryGetValue(a.AccountId, out var acc);
            shopkeepers.TryGetValue(a.AccountId, out var sk);
            Shop? shop = null;
            if (sk != null) shops.TryGetValue(sk.Id, out shop);

            var myConversions = conversions.Where(c => c.AffiliateId == a.Id).ToList();
            var myReferredShops = myConversions.Select(ParseShopId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var activeStores = myReferredShops.Count(sid => activeProductShopIds.Contains(sid));

            return new
            {
                id = a.Id,
                accountId = a.AccountId,
                shopId = shop?.Id,
                shopName = shop?.Name ?? sk?.Name ?? acc?.Username ?? "Storefront",
                shopSlug = shop?.Slug ?? "",
                shopOwner = sk?.Name ?? acc?.Username ?? "Merchant",
                sellerName = sk?.Name ?? acc?.Username ?? "Merchant",
                shopEmail = acc?.Email ?? "",
                email = acc?.Email ?? "",
                phone = sk?.Phone ?? shop?.Phone ?? "",
                code = a.ReferralCode,
                referralCode = a.ReferralCode,
                clicksCount = a.TotalClicks,
                totalClicks = a.TotalClicks,
                conversionsCount = myReferredShops.Count > 0 ? myReferredShops.Count : a.TotalConversions,
                totalConversions = myReferredShops.Count > 0 ? myReferredShops.Count : a.TotalConversions,
                activeStoresCount = activeStores,
                activeConversions = activeStores,
                totalEarned = a.TotalEarned,
                isActive = a.IsActive,
                createdAt = a.CreatedAt
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Get details of an affiliate and all their referred stores.
    /// </summary>
    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> GetAffiliateDetails(Guid id, CancellationToken cancellationToken)
    {
        var affiliate = await _context.Affiliates
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (affiliate is null)
            return NotFound(new { error = "Affiliate not found." });

        var acc = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == affiliate.AccountId, cancellationToken);

        var sk = await _context.Shopkeepers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountId == affiliate.AccountId, cancellationToken);

        Shop? shop = null;
        if (sk != null)
        {
            shop = await _context.Shops
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShopkeeperId == sk.Id && s.DeletedAt == null, cancellationToken);
        }

        var conversions = await _context.AffiliateClicks
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id && c.Converted)
            .OrderByDescending(c => c.ClickedAt)
            .ToListAsync(cancellationToken);

        var shopIds = conversions
            .Select(ParseShopId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var referredShopsList = new List<object>();

        if (shopIds.Any())
        {
            var refShops = await _context.Shops
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => shopIds.Contains(s.Id) && s.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var refSkIds = refShops.Select(s => s.ShopkeeperId).Distinct().ToList();

            var refShopkeepers = await _context.Shopkeepers
                .AsNoTracking()
                .Where(s => refSkIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken);

            var refAccountIds = refShopkeepers.Values.Select(s => s.AccountId).Distinct().ToList();

            var refAccounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => refAccountIds.Contains(a.Id))
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
                var rShop = refShops.FirstOrDefault(s => s.Id == sId.Value);
                if (rShop is null) continue;

                refShopkeepers.TryGetValue(rShop.ShopkeeperId, out var rSk);
                Account? rAcc = null;
                if (rSk != null) refAccounts.TryGetValue(rSk.AccountId, out rAcc);

                var pCount = productCounts.TryGetValue(rShop.Id, out var pc) ? pc : rShop.TotalProducts;

                referredShopsList.Add(new
                {
                    id = rShop.Id,
                    shopId = rShop.Id,
                    shopName = rShop.Name,
                    shopSlug = rShop.Slug,
                    ownerName = rSk?.Name ?? rAcc?.Username ?? "Merchant",
                    sellerName = rSk?.Name ?? rAcc?.Username ?? "Merchant",
                    email = rAcc?.Email ?? "",
                    phoneNumber = rSk?.Phone ?? rShop.Phone ?? "",
                    phone = rSk?.Phone ?? rShop.Phone ?? "",
                    status = rSk?.Status.ToString() ?? "Pending",
                    isApproved = rSk?.Status == ShopkeeperApprovalStatus.Approved,
                    productsCount = pCount,
                    totalProducts = pCount,
                    hasListedProducts = pCount > 0,
                    registeredAt = conv.ClickedAt
                });
            }
        }

        var activeCount = referredShopsList.Count(s =>
        {
            var prop = s.GetType().GetProperty("hasListedProducts");
            return prop != null && (bool)prop.GetValue(s)!;
        });

        return Ok(new
        {
            affiliate = new
            {
                id = affiliate.Id,
                accountId = affiliate.AccountId,
                shopId = shop?.Id,
                shopName = shop?.Name ?? sk?.Name ?? acc?.Username ?? "Storefront",
                shopSlug = shop?.Slug ?? "",
                shopOwner = sk?.Name ?? acc?.Username ?? "Merchant",
                sellerName = sk?.Name ?? acc?.Username ?? "Merchant",
                shopEmail = acc?.Email ?? "",
                email = acc?.Email ?? "",
                phone = sk?.Phone ?? shop?.Phone ?? "",
                code = affiliate.ReferralCode,
                referralCode = affiliate.ReferralCode,
                clicksCount = affiliate.TotalClicks,
                totalClicks = affiliate.TotalClicks,
                conversionsCount = referredShopsList.Count > 0 ? referredShopsList.Count : affiliate.TotalConversions,
                totalConversions = referredShopsList.Count > 0 ? referredShopsList.Count : affiliate.TotalConversions,
                activeStoresCount = activeCount,
                activeConversions = activeCount,
                totalEarned = affiliate.TotalEarned,
                isActive = affiliate.IsActive,
                createdAt = affiliate.CreatedAt
            },
            referredStores = referredShopsList,
            referredShops = referredShopsList
        });
    }

    private static Guid? ParseShopId(Eseller.Domain.Entities.Affiliate.AffiliateClick c)
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
}
