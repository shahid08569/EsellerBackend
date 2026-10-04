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

        // ═══════════════════════════════════════════════════════════
        // SHOPS
        // ═══════════════════════════════════════════════════════════
        var totalShops = await _context.Shops.CountAsync(cancellationToken);
        var activeShops = await _context.Shops
            .CountAsync(s => s.IsActive, cancellationToken);

        var pendingShops = await _context.Shopkeepers
            .CountAsync(sk => sk.Status == ShopkeeperApprovalStatus.Pending, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // PRODUCTS
        // ═══════════════════════════════════════════════════════════
        var totalProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p => p.DeletedAt == null, cancellationToken);

        var approvedProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p => p.IsApproved && p.DeletedAt == null, cancellationToken);

        var rejectedProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p => !p.IsApproved
                          && p.RejectionReason != null
                          && p.DeletedAt == null, cancellationToken);

        var pendingProducts = totalProducts - approvedProducts - rejectedProducts;

        // ═══════════════════════════════════════════════════════════
        // ORDERS
        // ═══════════════════════════════════════════════════════════
        var totalOrders = await _context.OrderRequests.CountAsync(cancellationToken);
        var pendingOrders = await _context.OrderRequests
            .CountAsync(o => o.Status == OrderStatus.Pending, cancellationToken);
        var deliveredOrders = await _context.OrderRequests
            .CountAsync(o => o.Status == OrderStatus.Delivered, cancellationToken);
        var cancelledOrders = await _context.OrderRequests
            .CountAsync(o => o.Status == OrderStatus.Cancelled, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // REVENUE (Total + Delivered)
        // ═══════════════════════════════════════════════════════════
        var totalRevenue = await _context.OrderRequestItems
            .SumAsync(i => (decimal?)i.TotalPriceSnapshot, cancellationToken) ?? 0m;

        var deliveredOrderIds = await _context.OrderRequests
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var deliveredRevenue = await _context.OrderRequestItems
            .Where(i => deliveredOrderIds.Contains(i.OrderRequestId))
            .SumAsync(i => (decimal?)i.TotalPriceSnapshot, cancellationToken) ?? 0m;

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
        var lastOrderAt = await _context.OrderRequests
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => (DateTime?)o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

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
        // DYNAMIC MONTHLY REVENUE (last 12 months, real data only)
        // ═══════════════════════════════════════════════════════════
        var nowUtc = DateTime.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var ordersWindow = await _context.OrderRequests
            .Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.Id, o.CreatedAt })
            .ToListAsync(cancellationToken);

        var orderIdsWindow = ordersWindow.Select(o => o.Id).ToList();
        var itemsWindow = await _context.OrderRequestItems
            .Where(i => orderIdsWindow.Contains(i.OrderRequestId))
            .Select(i => new { i.OrderRequestId, i.TotalPriceSnapshot })
            .ToListAsync(cancellationToken);

        var monthlyRevenue = new List<MonthlyRevenuePointDto>();
        decimal prevRev = 0;
        for (int i = 0; i < 12; i++)
        {
            var cursor = monthStart.AddMonths(i);
            var mOrderIds = ordersWindow
                .Where(o => o.CreatedAt.Year == cursor.Year && o.CreatedAt.Month == cursor.Month)
                .Select(o => o.Id)
                .ToHashSet();
            var rev = itemsWindow.Where(it => mOrderIds.Contains(it.OrderRequestId)).Sum(it => it.TotalPriceSnapshot);
            decimal growth = prevRev > 0 ? Math.Round(((rev - prevRev) / prevRev) * 100m, 1) : 0m;
            monthlyRevenue.Add(new MonthlyRevenuePointDto(cursor.ToString("MMM"), rev, growth));
            prevRev = rev;
        }

        // ═══════════════════════════════════════════════════════════
        // DYNAMIC WEEKLY ORDERS (last 7 days, real counts)
        // ═══════════════════════════════════════════════════════════
        var oneWeekAgo = DateTime.UtcNow.Date.AddDays(-6);
        var ordersLastWeek = await _context.OrderRequests
            .Where(o => o.CreatedAt >= oneWeekAgo)
            .Select(o => new { o.CreatedAt.DayOfWeek })
            .ToListAsync(cancellationToken);

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
            new DayOrderCountDto(d.Day, ordersLastWeek.Count(o => o.DayOfWeek == d.Dow))
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