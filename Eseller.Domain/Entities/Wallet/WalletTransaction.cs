using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Wallet;

public class WalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public WalletDirection Direction { get; set; }
    public WalletTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}