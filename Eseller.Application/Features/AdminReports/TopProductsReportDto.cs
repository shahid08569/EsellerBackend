namespace Eseller.Application.Features.AdminReports;

public record TopProductsReportDto(
    DateTime FromDate,
    DateTime ToDate,
    int Limit,
    List<TopProductItemDto> Products);

public record TopProductItemDto(
    Guid ProductId,
    string ProductName,
    string? ProductSlug,
    int TotalSold,
    decimal TotalRevenue,
    int OrderCount);