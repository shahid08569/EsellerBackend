using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Identity;
public class VerificationToken : BaseEntity
{
    public Guid AccountId { get; set; }
    public TokenPurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public VerificationChannel Channel { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}