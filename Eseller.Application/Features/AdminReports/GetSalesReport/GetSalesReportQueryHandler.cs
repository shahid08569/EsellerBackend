using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminReports.GetSalesReport;

public class GetSalesReportQueryHandler
    : IRequestHandler<GetSalesReportQuery, Result<SalesReportDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSalesReportQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SalesReportDto>> Handle(
        GetSalesReportQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<SalesReportDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<SalesReportDto>(
                "Only admins can view reports.", "FORBIDDEN");

        if (request.FromDate > request.ToDate)
            return Result.Failure<SalesReportDto>(
                "From date cannot be after to date.", "INVALID_DATE_RANGE");

        var orders = await _context.OrderRequests
            .Where(o => o.CreatedAt >= request.FromDate
                     && o.CreatedAt <= request.ToDate)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();

        var items = await _context.OrderRequestItems
            .Where(i => orderIds.Contains(i.OrderRequestId))
            .ToListAsync(cancellationToken);

        var totalRevenue = items.Sum(i => i.TotalPriceSnapshot);

        var deliveredOrderIds = orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => o.Id)
            .ToList();

        var deliveredRevenue = items
            .Where(i => deliveredOrderIds.Contains(i.OrderRequestId))
            .Sum(i => i.TotalPriceSnapshot);

        var deliveredCount = orders.Count(o => o.Status == OrderStatus.Delivered);
        var cancelledCount = orders.Count(o => o.Status == OrderStatus.Cancelled);

        var avgOrderValue = orders.Count > 0 ? totalRevenue / orders.Count : 0m;

        // Daily breakdown
        var dailySales = orders
            .GroupBy(o => o.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var dayOrderIds = g.Select(o => o.Id).ToList();
                var dayRevenue = items
                    .Where(i => dayOrderIds.Contains(i.OrderRequestId))
                    .Sum(i => i.TotalPriceSnapshot);

                return new DailySalesDto(
                    g.Key,
                    g.Count(),
                    dayRevenue);
            })
            .ToList();

        var dto = new SalesReportDto(
            request.FromDate,
            request.ToDate,
            orders.Count,
            deliveredCount,
            cancelledCount,
            totalRevenue,
            deliveredRevenue,
            Math.Round(avgOrderValue, 2),
            dailySales);

        return Result.Success(dto);
    }
}