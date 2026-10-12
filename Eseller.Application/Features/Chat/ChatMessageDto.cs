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
    DateTime SentAt,
    string? AttachmentUrl = null,
    string? AttachmentFileName = null,
    string? AttachmentContentType = null,
    long? AttachmentSizeBytes = null,
    bool IsEdited = false,
    DateTime? EditedAt = null,
    bool IsDeletedForEveryone = false);
