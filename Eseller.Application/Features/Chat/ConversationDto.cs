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
    DateTime OrderCreatedAt);