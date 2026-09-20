namespace Eseller.Application.Features.Inventory;

public record LowStockVariantDto(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string Sku,
    int StockQty,
    int LowStockThreshold,
    string? ImageUrl);