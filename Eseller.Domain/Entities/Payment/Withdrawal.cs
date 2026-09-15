using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Payment;

public class Withdrawal : AuditableEntity
{
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string? PayoutMethod { get; set; }
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}