using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.ProductGroups;

public record GetProductGroupsQuery(
    Guid? ShopId = null,
    string? Search = null
) : IRequest<Result<List<ProductGroupDto>>>;

public class GetProductGroupsQueryHandler
    : IRequestHandler<GetProductGroupsQuery, Result<List<ProductGroupDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProductGroupsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ProductGroupDto>>> Handle(
        GetProductGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ProductGroupListings
            .IgnoreQueryFilters()
            .Where(g => g.DeletedAt == null);

        if (request.ShopId.HasValue && request.ShopId.Value != Guid.Empty)
        {
            query = query.Where(g => g.ShopId == request.ShopId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(g => g.BatchName.ToLower().Contains(s));
        }

        var groups = await query
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        var shopIds = groups.Select(g => g.ShopId).Distinct().ToList();
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var batchIds = groups.Select(g => g.Id).ToList();
        var currentCounts = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ListingBatchId.HasValue && batchIds.Contains(p.ListingBatchId.Value) && p.DeletedAt == null)
            .GroupBy(p => p.ListingBatchId!.Value)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BatchId, x => x.Count, cancellationToken);

        var dtos = groups.Select(g =>
        {
            shops.TryGetValue(g.ShopId, out var shop);
            currentCounts.TryGetValue(g.Id, out var count);

            return new ProductGroupDto(
                g.Id,
                g.ShopId,
                shop?.Name ?? "Shop",
                shop?.Slug ?? "shop",
                g.BatchName,
                g.RequestedCount,
                g.DeliveredCount,
                count,
                g.CategoryFilter,
                g.BrandFilter,
                g.MinPrice,
                g.MaxPrice,
                g.CreatedAt,
                g.UpdatedAt
            );
        }).ToList();

        return Result.Success(dtos);
    }
}
