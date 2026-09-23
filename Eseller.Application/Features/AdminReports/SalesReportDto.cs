namespace Eseller.Application.Features.AdminReports;

public record SalesReportDto(
    DateTime FromDate,
    DateTime ToDate,
    int TotalOrders,
    int DeliveredOrders,
    int CancelledOrders,
    decimal TotalRevenue,
    decimal DeliveredRevenue,
    decimal AverageOrderValue,
    List<DailySalesDto> DailySales);

public record DailySalesDto(
    DateTime Date,
    int Orders,
    decimal Revenue);