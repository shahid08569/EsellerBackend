namespace Eseller.Application.Features.Compare;

public record CompareDetailsDto(
    List<CompareProductDetails> Products,
    List<CompareAttributeGroup> AttributeGroups);

public record CompareProductDetails(
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string? ProductImage,
    decimal BasePrice,
    decimal? DiscountedPrice,
    string? BrandName,
    string? CategoryName,
    decimal? AvgRating,
    string ShopName,
    string ShopSlug,
    int TotalStock,
    bool InStock,
    List<CompareVariantSummary> Variants);

public record CompareVariantSummary(
    Guid VariantId,
    string Sku,
    decimal Price,
    int StockQty,
    List<string> AttributeValues);

public record CompareAttributeGroup(
    string AttributeName,
    List<string?> Values);   // Per product, same order