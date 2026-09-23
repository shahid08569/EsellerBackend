using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.FlashSales.GetActiveFlashSales;

public class GetActiveFlashSalesQueryHandler
    : IRequestHandler<GetActiveFlashSalesQuery, Result<PagedList<FlashSaleDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public GetActiveFlashSalesQueryHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<FlashSaleDto>>> Handle(
        GetActiveFlashSalesQuery request,
        CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;

        var query = _context.FlashSales
            .Where(f => f.IsActive
                     && f.StartDate <= now
                     && f.EndDate >= now)
            .OrderByDescending(f => f.StartDate);

        var totalCount = await query.CountAsync(cancellationToken);

        var flashSales = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var flashSaleIds = flashSales.Select(f => f.Id).ToList();

        var items = await _context.FlashSaleProducts
            .Where(fp => flashSaleIds.Contains(fp.FlashSaleId))
            .ToListAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id)
                     && p.IsActive
                     && p.IsApproved
                     && p.DeletedAt == null)
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var result = flashSales.Select(f =>
        {
            var productDtos = items
                .Where(fp => fp.FlashSaleId == f.Id && products.ContainsKey(fp.ProductId))
                .Select(fp => new FlashSaleProductDto(
                    fp.Id,
                    fp.ProductId,
                    products.GetValueOrDefault(fp.ProductId, string.Empty),
                    (int)fp.DiscountType,
                    fp.DiscountValue))
                .ToList();

            return new FlashSaleDto(
                f.Id,
                f.Name,
                f.StartDate,
                f.EndDate,
                f.IsActive,
                true,
                productDtos.Count,
                productDtos,
                f.CreatedAt,
                f.UpdatedAt);
        }).ToList();

        var pagedList = new PagedList<FlashSaleDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}