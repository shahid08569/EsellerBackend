using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Wallet;

public class Wallet : AuditableEntity
{
    public Guid AccountId { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Seller-facing pending commission total (Management can set absolutely).</summary>
    public decimal PendingEarnings { get; set; }

    /// <summary>Seller-facing available commission total (Management can set absolutely).</summary>
    public decimal AvailableEarnings { get; set; }
}