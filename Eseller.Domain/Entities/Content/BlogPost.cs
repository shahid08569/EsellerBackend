using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Content;

public class BlogPost : AuditableEntity
{
    public Guid? BlogCategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public Guid? CreatedByAccountId { get; set; }
}