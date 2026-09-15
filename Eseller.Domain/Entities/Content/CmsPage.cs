using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Content;

public class CmsPage : AuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}