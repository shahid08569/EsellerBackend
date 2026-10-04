namespace Eseller.Domain.Enums;

public enum NotificationType
{
    OrderPlaced = 1,
    OrderConfirmed = 2,
    OrderShipped = 3,
    OrderDelivered = 4,
    OrderCancelled = 5,
    ShopApproved = 6,
    ShopRejected = 7,
    ProductApproved = 8,
    ProductRejected = 9,
    WithdrawalRequested = 10,
    WithdrawalProcessed = 11,
    ReturnRequested = 12,
    RefundProcessed = 13,
    NewChatMessage = 14,
    NewSellerRegistration = 15,
    NewOrderForSuperAdmin = 16,
    TierUpgradeApproved = 17,
    TierUpgradeRejected = 18,
    SystemAlert = 19
}