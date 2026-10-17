using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminDashboard.GetPlatformDashboard;

public class GetPlatformDashboardQueryHandler
    : IRequestHandler<GetPlatformDashboardQuery, Result<PlatformDashboardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPlatformDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PlatformDashboardDto>> Handle(
        GetPlatformDashboardQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<PlatformDashboardDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // 2. Role check — SuperAdmin/Partner only
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PlatformDashboardDto>(
                "Only admins can view platform dashboard.", "FORBIDDEN");

        Guid? partnerId = null;
        if (role == RoleType.Partner)
        {
            var partner = await _context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == _currentUserService.AccountId.Value, cancellationToken);
            partnerId = partner?.Id ?? Guid.Empty;
        }

        // ═══════════════════════════════════════════════════════════
        // SHOPS
        // ═══════════════════════════════════════════════════════════
        var shopsQuery = _context.Shops.AsQueryable();
        var shopkeepersQuery = _context.Shopkeepers.AsQueryable();
        if (partnerId.HasValue)
        {
            shopsQuery = shopsQuery.Where(s => s.PartnerId == partnerId.Value);
            shopkeepersQuery = shopkeepersQuery.Where(sk => sk.PartnerId == partnerId.Value);
        }

        var totalShops = await shopsQuery.CountAsync(cancellationToken);
        var activeShops = await shopsQuery
            .CountAsync(s => s.IsActive, cancellationToken);

        var pendingShops = await shopkeepersQuery
            .CountAsync(sk => sk.Status == ShopkeeperApprovalStatus.Pending, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // PRODUCTS
        // ═══════════════════════════════════════════════════════════
        var productsQuery = _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.DeletedAt == null);

        if (partnerId.HasValue)
        {
            var partnerShopIds = _context.Shops.IgnoreQueryFilters()
                .Where(s => s.PartnerId == partnerId.Value && s.DeletedAt == null)
                .Select(s => s.Id);
            productsQuery = productsQuery.Where(p => partnerShopIds.Contains(p.ShopId));
        }

        var totalProducts = await productsQuery.CountAsync(cancellationToken);

        var approvedProducts = await productsQuery
            .CountAsync(p => p.IsApproved, cancellationToken);

        var rejectedProducts = await productsQuery
            .CountAsync(p => !p.IsApproved
                          && p.RejectionReason != null, cancellationToken);

        var pendingProducts = totalProducts - approvedProducts - rejectedProducts;

        // ═══════════════════════════════════════════════════════════
        // ORDERS — commerce only (same filter as Orders Hub)
        // Single load → in-memory counts (fast + matches /orders list).
        // ═══════════════════════════════════════════════════════════
        var commerceOrderQuery = _context.OrderRequests
            .AsNoTracking()
            .Where(o => o.ShippingAddress == null
                || (!o.ShippingAddress.StartsWith("SHOP_SUPPORT_")
                    && !o.ShippingAddress.StartsWith("SHOP_CHAT_")
                    && !o.ShippingAddress.StartsWith("CUSTOMER_SUPPORT_")
                    && o.ShippingAddress != "Live Support Inquiry"))
            .Where(o => _context.SellerOrders.Any(so => so.OrderRequestId == o.Id)
                        || _context.OrderRequestItems.Any(i => i.OrderRequestId == o.Id));

        if (partnerId.HasValue)
        {
            var partnerShopIds = _context.Shops.IgnoreQueryFilters()
                .Where(s => s.PartnerId == partnerId.Value && s.DeletedAt == null)
                .Select(s => s.Id);
            var partnerOrderIds = _context.SellerOrders
                .Where(so => partnerShopIds.Contains(so.ShopId))
                .Select(so => so.OrderRequestId);
            commerceOrderQuery = commerceOrderQuery.Where(o => partnerOrderIds.Contains(o.Id));
        }

        var commerceRows = await commerceOrderQuery
            .Select(o => new { o.Id, o.Status, o.CreatedAt })
            .ToListAsync(cancellationToken);

        var totalOrders = commerceRows.Count;
        // New = still Pending (needs first action)
        var newOrders = commerceRows.Count(o => o.Status == OrderStatus.Pending);
        // Pending = open / not finished (everything except Delivered + Cancelled)
        var pendingOrders = commerceRows.Count(o =>
            o.Status != OrderStatus.Delivered
            && o.Status != OrderStatus.Cancelled
            && o.Status != OrderStatus.Returned
            && o.Status != OrderStatus.Refunded);
        var deliveredOrders = commerceRows.Count(o => o.Status == OrderStatus.Delivered);
        var cancelledOrders = commerceRows.Count(o => o.Status == OrderStatus.Cancelled);

        var commerceOrderIds = commerceRows.Select(o => o.Id).ToList();
        var deliveredOrderIds = commerceRows
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => o.Id)
            .ToList();

        // ═══════════════════════════════════════════════════════════
        // REVENUE (Total + Delivered) — commerce order lines only
        // ═══════════════════════════════════════════════════════════
        decimal totalRevenue = 0m;
        decimal deliveredRevenue = 0m;
        if (commerceOrderIds.Count > 0)
        {
            var lineTotals = await _context.OrderRequestItems
                .AsNoTracking()
                .Where(i => commerceOrderIds.Contains(i.OrderRequestId))
                .Select(i => new { i.OrderRequestId, i.TotalPriceSnapshot })
                .ToListAsync(cancellationToken);

            totalRevenue = lineTotals.Sum(i => i.TotalPriceSnapshot);
            if (deliveredOrderIds.Count > 0)
            {
                var deliveredSet = deliveredOrderIds.ToHashSet();
                deliveredRevenue = lineTotals
                    .Where(i => deliveredSet.Contains(i.OrderRequestId))
                    .Sum(i => i.TotalPriceSnapshot);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // AFFILIATES
        // ═══════════════════════════════════════════════════════════
        var totalAffiliates = await _context.Affiliates.CountAsync(cancellationToken);
        var activeAffiliates = await _context.Affiliates
            .CountAsync(a => a.IsActive, cancellationToken);

        var totalCommissionsPaid = await _context.AffiliateCommissions
            .Where(c => c.Status == AffiliateCommissionStatus.Paid)
            .SumAsync(c => (decimal?)c.CommissionAmount, cancellationToken) ?? 0m;

        var totalCommissionsPending = await _context.AffiliateCommissions
            .Where(c => c.Status == AffiliateCommissionStatus.Pending
                     || c.Status == AffiliateCommissionStatus.Approved)
            .SumAsync(c => (decimal?)c.CommissionAmount, cancellationToken) ?? 0m;

        // ═══════════════════════════════════════════════════════════
        // WITHDRAWALS
        // ═══════════════════════════════════════════════════════════
        var pendingWithdrawals = await _context.Withdrawals
            .CountAsync(w => w.Status == WithdrawalStatus.Pending, cancellationToken);

        var pendingWithdrawalsAmount = await _context.Withdrawals
            .Where(w => w.Status == WithdrawalStatus.Pending)
            .SumAsync(w => (decimal?)w.Amount, cancellationToken) ?? 0m;

        // ═══════════════════════════════════════════════════════════
        // USERS
        // ═══════════════════════════════════════════════════════════
        var totalCustomers = await _context.Accounts
            .CountAsync(a => a.RoleType == RoleType.User, cancellationToken);

        var totalShopkeepers = await _context.Accounts
            .CountAsync(a => a.RoleType == RoleType.Shopkeeper, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // RECENT ACTIVITY
        // ═══════════════════════════════════════════════════════════
        DateTime? lastOrderAt = commerceRows.Count == 0
            ? null
            : commerceRows.Max(o => o.CreatedAt);

        var lastShopRegisteredAt = await _context.Shops
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => (DateTime?)s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastAffiliateRegisteredAt = await _context.Affiliates
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (DateTime?)a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // DYNAMIC LOW STOCK ALERTS (Stock <= 5)
        // ═══════════════════════════════════════════════════════════
        var lowStockVariants = await _context.ProductVariants
            .Where(v => v.DeletedAt == null && v.StockQty <= 5)
            .OrderBy(v => v.StockQty)
            .Take(6)
            .ToListAsync(cancellationToken);

        var variantProdIds = lowStockVariants.Select(v => v.ProductId).Distinct().ToList();
        var prods = await _context.Products
            .Where(p => variantProdIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var catIds = prods.Values.Select(p => p.CategoryId).Distinct().ToList();
        var cats = await _context.Categories
            .Where(c => catIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var lowStockAlerts = lowStockVariants.Select(v =>
        {
            prods.TryGetValue(v.ProductId, out var prod);
            var catName = prod != null && cats.TryGetValue(prod.CategoryId, out var cName) ? cName : "General";
            return new LowStockItemDto(
                v.ProductId,
                prod?.Name ?? "Product Item",
                string.IsNullOrEmpty(v.Sku) ? ("SKU-" + v.Id.ToString().Substring(0, 6).ToUpper()) : v.Sku,
                catName,
                v.StockQty,
                v.ImageUrl
            );
        }).ToList();

        // ═══════════════════════════════════════════════════════════
        // DYNAMIC MONTHLY REVENUE + WEEKLY ORDERS (from in-memory rows)
        // ═══════════════════════════════════════════════════════════
        var nowUtc = DateTime.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var ordersWindow = commerceRows
            .Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.Id, o.CreatedAt })
            .ToList();

        Dictionary<Guid, decimal> itemSumByOrder = new();
        if (ordersWindow.Count > 0)
        {
            var orderIdsWindow = ordersWindow.Select(o => o.Id).ToList();
            var itemsWindow = await _context.OrderRequestItems
                .AsNoTracking()
                .Where(i => orderIdsWindow.Contains(i.OrderRequestId))
                .GroupBy(i => i.OrderRequestId)
                .Select(g => new { OrderRequestId = g.Key, Total = g.Sum(x => x.TotalPriceSnapshot) })
                .ToListAsync(cancellationToken);
            itemSumByOrder = itemsWindow.ToDictionary(x => x.OrderRequestId, x => x.Total);
        }

        var monthlyRevenue = new List<MonthlyRevenuePointDto>();
        decimal prevRev = 0;
        for (int i = 0; i < 12; i++)
        {
            var cursor = monthStart.AddMonths(i);
            var mOrderIds = ordersWindow
                .Where(o => o.CreatedAt.Year == cursor.Year && o.CreatedAt.Month == cursor.Month)
                .Select(o => o.Id);
            var rev = mOrderIds.Sum(id => itemSumByOrder.GetValueOrDefault(id));
            decimal growth = prevRev > 0 ? Math.Round(((rev - prevRev) / prevRev) * 100m, 1) : 0m;
            monthlyRevenue.Add(new MonthlyRevenuePointDto(cursor.ToString("MMM"), rev, growth));
            prevRev = rev;
        }

        var oneWeekAgo = DateTime.UtcNow.Date.AddDays(-6);
        var dayMappings = new[]
        {
            new { Day = "Sat", Dow = DayOfWeek.Saturday },
            new { Day = "Sun", Dow = DayOfWeek.Sunday },
            new { Day = "Mon", Dow = DayOfWeek.Monday },
            new { Day = "Tue", Dow = DayOfWeek.Tuesday },
            new { Day = "Wed", Dow = DayOfWeek.Wednesday },
            new { Day = "Thu", Dow = DayOfWeek.Thursday },
            new { Day = "Fri", Dow = DayOfWeek.Friday }
        };

        var weeklyOrders = dayMappings.Select(d =>
            new DayOrderCountDto(
                d.Day,
                commerceRows.Count(o => o.CreatedAt >= oneWeekAgo && o.CreatedAt.DayOfWeek == d.Dow))
        ).ToList();

        // ═══════════════════════════════════════════════════════════
        // BUILD DTO
        // ═══════════════════════════════════════════════════════════
        var dto = new PlatformDashboardDto(
            totalShops,
            activeShops,
            pendingShops,

            totalProducts,
            approvedProducts,
            pendingProducts,
            rejectedProducts,

            totalOrders,
            newOrders,
            pendingOrders,
            deliveredOrders,
            cancelledOrders,

            totalRevenue,
            deliveredRevenue,

            totalAffiliates,
            activeAffiliates,
            totalCommissionsPaid,
            totalCommissionsPending,

            pendingWithdrawals,
            pendingWithdrawalsAmount,

            totalCustomers,
            totalShopkeepers,

            lastOrderAt,
            lastShopRegisteredAt,
            lastAffiliateRegisteredAt,

            RevenueGrowthPercent: monthlyRevenue.Count >= 2
                ? monthlyRevenue[^1].GrowthPercent
                : 0m,
            OrdersGrowthPercent: weeklyOrders.Sum(w => w.Orders) > 0
                ? Math.Round(weeklyOrders.Average(w => (decimal)w.Orders), 1)
                : 0m,
            ShipmentsGrowthPercent: pendingOrders > 0
                ? -Math.Round((decimal)pendingOrders / Math.Max(totalOrders, 1) * 100m, 1)
                : 0m,
            CustomersGrowthPercent: totalCustomers > 0
                ? Math.Round((decimal)totalCustomers / Math.Max(totalCustomers + totalShopkeepers, 1) * 100m, 1)
                : 0m,

            MonthlyRevenue: monthlyRevenue,
            WeeklyOrders: weeklyOrders,
            LowStockAlerts: lowStockAlerts);

        return Result.Success(dto);
    }
}