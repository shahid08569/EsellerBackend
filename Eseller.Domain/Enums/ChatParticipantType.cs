namespace Eseller.Domain.Enums;

/// <summary>
/// Counterparty of a Super Admin support conversation.
/// Super Admin is implicit (not stored as a participant row).
/// </summary>
public enum ChatParticipantType
{
    Customer = 1,
    Seller = 2
}
