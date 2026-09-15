using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Orders;

public class ChatMessage : BaseEntity
{
    public Guid OrderRequestId { get; set; }
    public Guid SenderAccountId { get; set; }
    public ChatSenderRole SenderRole { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}