using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Content;

public class BlogCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}