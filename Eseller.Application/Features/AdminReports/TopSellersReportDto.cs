namespace Eseller.Application.Features.AdminReports;

public record TopSellersReportDto(
    DateTime FromDate,
    DateTime ToDate,
    int Limit,
    List<TopSellerItemDto> Sellers);

public record TopSellerItemDto(
    Guid ShopId,
    string ShopName,
    string ShopSlug,
    int TotalOrders,
    int TotalItemsSold,
    decimal TotalRevenue);