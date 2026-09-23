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
    DateTime? LastAffiliateRegisteredAt);