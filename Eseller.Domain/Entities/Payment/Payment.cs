using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Payment;

public class Payment : AuditableEntity
{
    public Guid OrderRequestId { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Provider { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string Currency { get; set; } = "PKR";
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
}