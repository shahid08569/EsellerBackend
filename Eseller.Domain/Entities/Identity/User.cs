using Eseller.Domain.Common;
using Eseller.Domain.Enums;

namespace Eseller.Domain.Entities.Identity;

public class User : AuditableEntity
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>SuperAdmin must approve before the customer can place orders.</summary>
    public UserApprovalStatus Status { get; set; } = UserApprovalStatus.Pending;

    public Guid? ApprovedByAccountId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}
