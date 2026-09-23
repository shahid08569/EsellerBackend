using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.FlashSales.GetFlashSales;

public class GetFlashSalesQueryHandler
    : IRequestHandler<GetFlashSalesQuery, Result<PagedList<FlashSaleDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetFlashSalesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<FlashSaleDto>>> Handle(
        GetFlashSalesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<FlashSaleDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<FlashSaleDto>>(
                "Only admins can view flash sales.", "FORBIDDEN");

        var query = _context.FlashSales.AsQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(f => f.IsActive == request.IsActive.Value);

        query = query.OrderByDescending(f => f.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var flashSales = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var flashSaleIds = flashSales.Select(f => f.Id).ToList();

        var flashSaleProducts = await _context.FlashSaleProducts
            .Where(fp => flashSaleIds.Contains(fp.FlashSaleId))
            .ToListAsync(cancellationToken);

        var productIds = flashSaleProducts.Select(fp => fp.ProductId).Distinct().ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var now = _dateTime.UtcNow;

        var result = flashSales.Select(f =>
        {
            var items = flashSaleProducts
                .Where(fp => fp.FlashSaleId == f.Id)
                .Select(fp => new FlashSaleProductDto(
                    fp.Id,
                    fp.ProductId,
                    products.GetValueOrDefault(fp.ProductId, string.Empty),
                    (int)fp.DiscountType,
                    fp.DiscountValue))
                .ToList();

            var isCurrentlyActive = f.IsActive
                && f.StartDate <= now
                && f.EndDate >= now;

            return new FlashSaleDto(
                f.Id,
                f.Name,
                f.StartDate,
                f.EndDate,
                f.IsActive,
                isCurrentlyActive,
                items.Count,
                items,
                f.CreatedAt,
                f.UpdatedAt);
        }).ToList();

        var pagedList = new PagedList<FlashSaleDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}