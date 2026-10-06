using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Chat;

/// <summary>
/// One support thread: Customer|Seller ↔ Super Admin.
/// Unique on (ParticipantId, ParticipantType).
/// </summary>
public class Conversation : AuditableEntity
{
    /// <summary>Customer User.Id or Seller Shop.Id.</summary>
    public Guid ParticipantId { get; set; }

    public ChatParticipantType ParticipantType { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
