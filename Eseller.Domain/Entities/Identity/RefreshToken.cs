using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Identity;

public class RefreshToken : BaseEntity
{
    public Guid AccountId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public void Revoke(DateTime timestamp)
    {
        RevokedAt = timestamp;
    }

    public void SetReplacedBy(Guid newTokenId)
    {
        ReplacedByTokenId = newTokenId;
    }

}