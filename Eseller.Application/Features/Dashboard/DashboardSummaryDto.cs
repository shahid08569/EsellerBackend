namespace Eseller.Application.Features.Dashboard;

public record DashboardSummaryDto(
    int TotalOrders,
    int PendingOrders,
    int DeliveredOrders,
    int CancelledOrders,
    int WishlistCount,
    int CompareCount,
    int AddressCount,
    int UnreadNotifications,
    int UnreadChatMessages,
    decimal TotalSpent);