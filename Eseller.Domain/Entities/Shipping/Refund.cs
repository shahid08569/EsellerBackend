using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Shipping;

public class Refund : AuditableEntity
{
    public Guid? PaymentId { get; set; }
    public Guid? ReturnRequestId { get; set; }
    public decimal Amount { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public string? RefundMethod { get; set; }
    public string? ProviderReference { get; set; }
    public Guid? ProcessedByAccountId { get; set; }
}