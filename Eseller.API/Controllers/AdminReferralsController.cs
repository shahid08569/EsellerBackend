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
        var affiliates = await _context.Affiliates
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
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
            .Where(c => affiliateIds.Contains(c.AffiliateId) && c.Converted && c.ProductId != null)
            .ToListAsync(cancellationToken);

        var referredShopIds = conversions
            .Where(c => c.ProductId.HasValue)
            .Select(c => c.ProductId!.Value)
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
            var myReferredShops = myConversions.Where(c => c.ProductId.HasValue).Select(c => c.ProductId!.Value).Distinct().ToList();
            var activeStores = myReferredShops.Count(sid => activeProductShopIds.Contains(sid));

            return new
            {
                id = a.Id,
                accountId = a.AccountId,
                sellerName = sk?.Name ?? acc?.Username ?? "Merchant",
                shopName = shop?.Name ?? "No Store",
                shopSlug = shop?.Slug ?? "",
                email = acc?.Email ?? "",
                phone = sk?.Phone ?? shop?.Phone ?? "",
                referralCode = a.ReferralCode,
                totalClicks = a.TotalClicks,
                totalConversions = myReferredShops.Count > 0 ? myReferredShops.Count : a.TotalConversions,
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
            .Where(c => c.AffiliateId == affiliate.Id && c.Converted && c.ProductId != null)
            .OrderByDescending(c => c.ClickedAt)
            .ToListAsync(cancellationToken);

        var shopIds = conversions
            .Where(c => c.ProductId.HasValue)
            .Select(c => c.ProductId!.Value)
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
                if (!conv.ProductId.HasValue) continue;
                var sId = conv.ProductId.Value;
                var rShop = refShops.FirstOrDefault(s => s.Id == sId);
                if (rShop is null) continue;

                refShopkeepers.TryGetValue(rShop.ShopkeeperId, out var rSk);
                Account? rAcc = null;
                if (rSk != null) refAccounts.TryGetValue(rSk.AccountId, out rAcc);

                var pCount = productCounts.TryGetValue(rShop.Id, out var pc) ? pc : rShop.TotalProducts;

                referredShopsList.Add(new
                {
                    shopId = rShop.Id,
                    shopName = rShop.Name,
                    shopSlug = rShop.Slug,
                    sellerName = rSk?.Name ?? rAcc?.Username ?? "Merchant",
                    email = rAcc?.Email ?? "",
                    phone = rSk?.Phone ?? rShop.Phone ?? "",
                    status = rSk?.Status.ToString() ?? "Pending",
                    isApproved = rSk?.Status == ShopkeeperApprovalStatus.Approved,
                    totalProducts = pCount,
                    hasListedProducts = pCount > 0,
                    registeredAt = conv.ClickedAt
                });
            }
        }

        return Ok(new
        {
            affiliate = new
            {
                id = affiliate.Id,
                accountId = affiliate.AccountId,
                sellerName = sk?.Name ?? acc?.Username ?? "Merchant",
                shopName = shop?.Name ?? "No Store",
                shopSlug = shop?.Slug ?? "",
                email = acc?.Email ?? "",
                phone = sk?.Phone ?? shop?.Phone ?? "",
                referralCode = affiliate.ReferralCode,
                totalClicks = affiliate.TotalClicks,
                totalConversions = referredShopsList.Count > 0 ? referredShopsList.Count : affiliate.TotalConversions,
                activeConversions = referredShopsList.Count(s => (bool)s.GetType().GetProperty("hasListedProducts")!.GetValue(s)!),
                totalEarned = affiliate.TotalEarned,
                isActive = affiliate.IsActive,
                createdAt = affiliate.CreatedAt
            },
            referredStores = referredShopsList
        });
    }
}
