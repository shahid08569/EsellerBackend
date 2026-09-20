namespace Eseller.Application.Features.ProductVariants;

public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    decimal Price,
    int StockQty,
    int LowStockThreshold,
    string? ImageUrl,
    bool IsActive,
    List<VariantAttributeDto> Attributes,
    DateTime CreatedAt);