namespace Eseller.Application.Features.Orders;

public record OrderRequestItemDto(
    Guid Id,
    Guid ProductId,
    Guid ProductVariantId,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    string ProductNameSnapshot,
    string Sku,
    string? VariantAttributesSnapshot,
    int Quantity,
    decimal UnitPriceSnapshot,
    decimal TotalPriceSnapshot,
    decimal PriceAtOrder,
    int? AvailableStock);