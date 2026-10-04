using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class Category : AuditableEntity, ISoftDelete
{
    public Guid? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }

    public bool IsFeaturedOnHomepage { get; set; } = false;
    public int HomepageDisplayOrder { get; set; } = 0;
}