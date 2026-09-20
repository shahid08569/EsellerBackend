using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Discounts.GetDiscounts;

public class GetDiscountsQueryHandler
    : IRequestHandler<GetDiscountsQuery, Result<PagedList<DiscountListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public GetDiscountsQueryHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<DiscountListDto>>> Handle(
        GetDiscountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Discounts.AsQueryable();

        // Filters
        if (request.ProductId.HasValue)
            query = query.Where(d => d.ProductId == request.ProductId.Value);

        if (request.ScopeType.HasValue)
            query = query.Where(d => d.ScopeType == request.ScopeType.Value);

        if (request.ScopeId.HasValue)
            query = query.Where(d => d.ScopeId == request.ScopeId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(d => d.IsActive == request.IsActive.Value);

        query = query.OrderByDescending(d => d.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var now = _dateTime.UtcNow;

        var discounts = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DiscountListDto(
                d.Id,
                d.ProductId,
                d.ProductId == null
                    ? null
                    : _context.Products.Where(p => p.Id == d.ProductId).Select(p => p.Name).FirstOrDefault(),
                d.ScopeType.ToString(),
                d.ScopeId,
                d.Type.ToString(),
                d.Value,
                d.StartDate,
                d.EndDate,
                d.IsActive,
                d.IsActive && d.StartDate <= now && d.EndDate >= now))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<DiscountListDto>(
            discounts,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}