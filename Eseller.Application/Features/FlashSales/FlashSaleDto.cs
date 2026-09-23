namespace Eseller.Application.Features.FlashSales;

public record FlashSaleDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    bool IsCurrentlyActive,
    int ProductCount,
    List<FlashSaleProductDto> Products,
    DateTime CreatedAt,
    DateTime UpdatedAt);