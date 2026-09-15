using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Content;

public class HomepageBanner : AuditableEntity
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}