using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Payment;

public class PaymentAttempt : BaseEntity
{
    public Guid PaymentId { get; set; }
    public int AttemptNumber { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ProviderResponse { get; set; } // JSON
    public DateTime CreatedAt { get; set; }
}