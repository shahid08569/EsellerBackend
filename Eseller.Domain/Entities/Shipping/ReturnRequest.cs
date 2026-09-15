using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Shipping;

public class ReturnRequest : AuditableEntity
{
    public Guid SellerOrderId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;
    public Guid? ProcessedByAccountId { get; set; }
}