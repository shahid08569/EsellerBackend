using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Chat;

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    /// <summary>Authenticated Account.Id from JWT — never trust client SenderId.</summary>
    public Guid SenderId { get; set; }

    public ChatSenderRole SenderRole { get; set; }

    /// <summary>Text body; may be empty when only a file is sent.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentSizeBytes { get; set; }
}
