namespace Eseller.Application.Features.Products.ProductGroups;

public record ProductGroupDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    string BatchName,
    int RequestedCount,
    int DeliveredCount,
    int CurrentProductCount,
    int ApprovedProductsCount,
    int PendingProductsCount,
    bool IsApproved,
    string? CategoryFilter,
    string? BrandFilter,
    decimal? MinPrice,
    decimal? MaxPrice,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ProductGroupDetailDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    string BatchName,
    int RequestedCount,
    int DeliveredCount,
    int CurrentProductCount,
    int ApprovedProductsCount,
    int PendingProductsCount,
    bool IsApproved,
    string? CategoryFilter,
    string? BrandFilter,
    decimal? MinPrice,
    decimal? MaxPrice,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<GroupProductCardDto> Products
);

public record GroupProductCardDto(
    Guid Id,
    Guid? SourceProductId,
    string Name,
    string Slug,
    decimal BasePrice,
    string? PrimaryImageUrl,
    Guid CategoryId,
    string? CategoryName,
    Guid? BrandId,
    string? BrandName,
    bool IsApproved,
    bool IsActive,
    int VariantsCount,
    DateTime CreatedAt
);
