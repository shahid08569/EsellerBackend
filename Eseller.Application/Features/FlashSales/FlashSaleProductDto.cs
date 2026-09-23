namespace Eseller.Application.Features.FlashSales;

public record FlashSaleProductDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int DiscountType,
    decimal DiscountValue);