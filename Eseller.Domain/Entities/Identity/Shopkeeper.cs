using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Identity;

public class Shopkeeper : AuditableEntity
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ShopkeeperApprovalStatus Status { get; set; } = ShopkeeperApprovalStatus.Pending;
    public Guid? ApprovedByAccountId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}