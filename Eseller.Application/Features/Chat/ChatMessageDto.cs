namespace Eseller.Application.Features.Chat;

public record ChatMessageDto(
    Guid Id,
    Guid OrderRequestId,
    Guid SenderAccountId,
    string SenderRole,
    string Message,
    bool IsRead,
    DateTime? ReadAt,
    DateTime? DeliveredAt,
    DateTime SentAt);