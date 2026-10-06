namespace Eseller.Domain.Enums;

/// <summary>
/// Allowed notification types only (Super Admin + Seller requirements).
/// </summary>
public enum NotificationType
{
    NewCustomer = 1,
    NewSeller = 2,
    NewOrder = 3,
    NewMessage = 4,
    ProductListingRequest = 5,
    ProductOutOfStock = 6,
    TierUpdated = 7,
    OrderDelivered = 8,
    PaymentCompleted = 9,
    CommissionEarned = 10,
    TierBadgeRequestUpdated = 11,
    ProductListingRequestUpdated = 12,
    NewProduct = 13
}
