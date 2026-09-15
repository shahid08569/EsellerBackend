using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class VariantAttributeValue : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string AttributeValue { get; set; } = string.Empty;
}