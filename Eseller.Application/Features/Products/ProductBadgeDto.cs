namespace Eseller.Application.Features.Products;

public record ProductBadgeDto(
    bool IsNew,
    bool IsBestSelling,
    bool IsHotSelling,
    bool IsFeatured,
    bool IsFlashSale,
    bool IsLowStock,
    bool IsOutOfStock,
    decimal? DiscountPercent,
    decimal? FlashSaleDiscountPercent,
    DateTime? FlashSaleEndDate);