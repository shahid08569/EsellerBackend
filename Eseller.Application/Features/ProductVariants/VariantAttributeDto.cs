namespace Eseller.Application.Features.ProductVariants;

public record VariantAttributeDto(
    Guid Id,
    string AttributeName,
    string AttributeValue);