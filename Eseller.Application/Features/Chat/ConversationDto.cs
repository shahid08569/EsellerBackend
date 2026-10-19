namespace Eseller.Application.Features.Chat;

public record ConversationDto(
    Guid OrderRequestId,
    Guid UserId,
    string CustomerName,
    string CustomerEmail,
    string OrderStatus,
    decimal OrderTotal,
    int TotalItems,
    string? LastMessage,
    Guid? LastSenderAccountId,
    DateTime? LastMessageAt,
    int UnreadCount,
    DateTime OrderCreatedAt,
    string? ContextProductId = null,
    string? ContextProductName = null,
    string? ContextProductImageUrl = null,
    decimal? ContextProductPrice = null,
    /// <summary>"Seller" (shop support), "Customer" (buyer support), "Partner", or "SellerPartner".</summary>
    string? ParticipantType = null,
    string? LogoUrl = null,
    bool IsReferred = false,
    Guid? PartnerId = null,
    string? PartnerName = null,
    bool IsReadOnly = false,
    Guid? ShopId = null);
