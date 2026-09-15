using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}