using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class ShopImage : BaseEntity
{
    public Guid ShopId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // logo / banner / gallery
}