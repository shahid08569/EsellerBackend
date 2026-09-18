using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.GetShops;

public class GetShopsQueryHandler
    : IRequestHandler<GetShopsQuery, Result<PagedList<ShopListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetShopsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<ShopListDto>>> Handle(
        GetShopsQuery request,
        CancellationToken cancellationToken)
    {
        // Public list — only active shops
        var query = _context.Shops
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var shops = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new ShopListDto(
                s.Id,
                s.Name,
                s.Slug,
                s.LogoUrl,
                s.City,
                s.Country,
                s.Rating,
                s.TotalProducts,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ShopListDto>(
            shops,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}