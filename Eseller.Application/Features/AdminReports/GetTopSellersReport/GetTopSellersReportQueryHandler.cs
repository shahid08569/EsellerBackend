using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminReports.GetTopSellersReport;

public class GetTopSellersReportQueryHandler
    : IRequestHandler<GetTopSellersReportQuery, Result<TopSellersReportDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTopSellersReportQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TopSellersReportDto>> Handle(
        GetTopSellersReportQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<TopSellersReportDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<TopSellersReportDto>(
                "Only admins can view reports.", "FORBIDDEN");

        if (request.FromDate > request.ToDate)
            return Result.Failure<TopSellersReportDto>(
                "From date cannot be after to date.", "INVALID_DATE_RANGE");

        var limit = request.Limit > 50 ? 50 : (request.Limit < 1 ? 10 : request.Limit);

        var items = await (
            from i in _context.OrderRequestItems
            join o in _context.OrderRequests on i.OrderRequestId equals o.Id
            join v in _context.ProductVariants on i.ProductVariantId equals v.Id
            join p in _context.Products on v.ProductId equals p.Id
            join s in _context.Shops on p.ShopId equals s.Id
            where o.CreatedAt >= request.FromDate
               && o.CreatedAt <= request.ToDate
               && o.Status == OrderStatus.Delivered
            select new
            {
                ShopId = s.Id,
                ShopName = s.Name,
                ShopSlug = s.Slug,
                Quantity = i.Quantity,
                TotalPrice = i.TotalPriceSnapshot,
                OrderId = o.Id
            }).ToListAsync(cancellationToken);

        var grouped = items
            .GroupBy(x => new { x.ShopId, x.ShopName, x.ShopSlug })
            .Select(g => new TopSellerItemDto(
                g.Key.ShopId,
                g.Key.ShopName,
                g.Key.ShopSlug,
                g.Select(x => x.OrderId).Distinct().Count(),
                g.Sum(x => x.Quantity),
                g.Sum(x => x.TotalPrice)))
            .OrderByDescending(s => s.TotalRevenue)
            .Take(limit)
            .ToList();

        var dto = new TopSellersReportDto(
            request.FromDate,
            request.ToDate,
            limit,
            grouped);

        return Result.Success(dto);
    }
}