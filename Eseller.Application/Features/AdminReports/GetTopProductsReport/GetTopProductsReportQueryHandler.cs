using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminReports.GetTopProductsReport;

public class GetTopProductsReportQueryHandler
    : IRequestHandler<GetTopProductsReportQuery, Result<TopProductsReportDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTopProductsReportQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TopProductsReportDto>> Handle(
        GetTopProductsReportQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<TopProductsReportDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<TopProductsReportDto>(
                "Only admins can view reports.", "FORBIDDEN");

        if (request.FromDate > request.ToDate)
            return Result.Failure<TopProductsReportDto>(
                "From date cannot be after to date.", "INVALID_DATE_RANGE");

        var limit = request.Limit > 50 ? 50 : (request.Limit < 1 ? 10 : request.Limit);

        var items = await (
            from i in _context.OrderRequestItems
            join o in _context.OrderRequests on i.OrderRequestId equals o.Id
            join v in _context.ProductVariants on i.ProductVariantId equals v.Id
            join p in _context.Products on v.ProductId equals p.Id
            where o.CreatedAt >= request.FromDate
               && o.CreatedAt <= request.ToDate
               && o.Status == OrderStatus.Delivered
            select new
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ProductSlug = p.Slug,
                Quantity = i.Quantity,
                TotalPrice = i.TotalPriceSnapshot,
                OrderId = o.Id
            }).ToListAsync(cancellationToken);

        var grouped = items
            .GroupBy(x => new { x.ProductId, x.ProductName, x.ProductSlug })
            .Select(g => new TopProductItemDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.ProductSlug,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.TotalPrice),
                g.Select(x => x.OrderId).Distinct().Count()))
            .OrderByDescending(p => p.TotalRevenue)
            .Take(limit)
            .ToList();

        var dto = new TopProductsReportDto(
            request.FromDate,
            request.ToDate,
            limit,
            grouped);

        return Result.Success(dto);
    }
}