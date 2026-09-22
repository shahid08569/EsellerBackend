using Eseller.Application.Features.ProductVariants;

namespace Eseller.Application.Features.Cart;

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    Guid ProductVariantId,
    string ProductName,
    string ProductSlug,
    string Sku,
    string? VariantImage,
    string? ProductImage,
    decimal Price,
    int Quantity,
    int AvailableStock,
    decimal TotalPrice,
    string ShopName,
    string ShopSlug,
    Guid ShopId,
    List<VariantAttributeDto> Attributes);