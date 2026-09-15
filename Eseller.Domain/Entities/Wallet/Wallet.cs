using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Wallet;

public class Wallet : AuditableEntity
{
    public Guid AccountId { get; set; }
    public decimal Balance { get; set; }
}