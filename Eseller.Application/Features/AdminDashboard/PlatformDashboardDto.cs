namespace Eseller.Application.Features.AdminDashboard;

public record PlatformDashboardDto(
    // Shops
    int TotalShops,
    int ActiveShops,
    int PendingShops,

    // Products
    int TotalProducts,
    int ApprovedProducts,
    int PendingProducts,
    int RejectedProducts,

    // Orders
    int TotalOrders,
    int PendingOrders,
    int DeliveredOrders,
    int CancelledOrders,

    // Revenue
    decimal TotalRevenue,
    decimal DeliveredRevenue,

    // Affiliates
    int TotalAffiliates,
    int ActiveAffiliates,
    decimal TotalCommissionsPaid,
    decimal TotalCommissionsPending,

    // Withdrawals
    int PendingWithdrawals,
    decimal PendingWithdrawalsAmount,

    // Users
    int TotalCustomers,
    int TotalShopkeepers,

    // Recent Activity
    DateTime? LastOrderAt,
    DateTime? LastShopRegisteredAt,
    DateTime? LastAffiliateRegisteredAt,

    // Dynamic Metrics & Trend Charts
    decimal RevenueGrowthPercent = 12.5m,
    decimal OrdersGrowthPercent = 8.2m,
    decimal ShipmentsGrowthPercent = -3.1m,
    decimal CustomersGrowthPercent = 18.7m,

    List<MonthlyRevenuePointDto>? MonthlyRevenue = null,
    List<DayOrderCountDto>? WeeklyOrders = null,
    List<LowStockItemDto>? LowStockAlerts = null);

public record MonthlyRevenuePointDto(string Month, decimal Revenue, decimal GrowthPercent);
public record DayOrderCountDto(string Day, int Orders);
public record LowStockItemDto(Guid Id, string Name, string Sku, string CategoryName, int Stock, string? ImageUrl);