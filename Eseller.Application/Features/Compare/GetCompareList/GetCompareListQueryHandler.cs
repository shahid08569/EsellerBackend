using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Compare.GetCompareList;

public class GetCompareListQueryHandler
    : IRequestHandler<GetCompareListQuery, Result<IReadOnlyList<CompareItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetCompareListQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<IReadOnlyList<CompareItemDto>>> Handle(
        GetCompareListQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<CompareItemDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<IReadOnlyList<CompareItemDto>>(
                "Only customers can use compare.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<IReadOnlyList<CompareItemDto>>(
                "User not found.", "USER_NOT_FOUND");

        var items = await _context.CompareItems
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return Result.Success<IReadOnlyList<CompareItemDto>>(new List<CompareItemDto>());

        var productIds = items.Select(c => c.ProductId).ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var shopIds = products.Values.Select(p => p.ShopId).Distinct().ToList();

        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var brandIds = products.Values
            .Where(p => p.BrandId.HasValue)
            .Select(p => p.BrandId!.Value)
            .Distinct()
            .ToList();

        var brands = await _context.Brands
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        // Get total stock per product
        var totalStocks = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => productIds.Contains(pv.ProductId)
                      && pv.IsActive
                      && pv.DeletedAt == null)
            .GroupBy(pv => pv.ProductId)
            .Select(g => new { ProductId = g.Key, TotalStock = g.Sum(pv => pv.StockQty) })
            .ToDictionaryAsync(x => x.ProductId, x => x.TotalStock, cancellationToken);

        // Active discounts
        var now = _dateTime.UtcNow;
        var activeDiscounts = await _context.Discounts
            .Where(d => d.IsActive
                     && d.ProductId.HasValue
                     && productIds.Contains(d.ProductId.Value)
                     && d.StartDate <= now
                     && d.EndDate >= now)
            .ToListAsync(cancellationToken);

        var discountsByProduct = activeDiscounts
            .Where(d => d.ProductId.HasValue)
            .GroupBy(d => d.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareItemDto>();
        var itemsToRemove = new List<Domain.Entities.Engagement.CompareItem>();

        foreach (var item in items)
        {
            var product = products.GetValueOrDefault(item.ProductId);

            if (product is null || product.DeletedAt != null)
            {
                itemsToRemove.Add(item);
                continue;
            }

            var shop = shops.GetValueOrDefault(product.ShopId);
            var brand = product.BrandId.HasValue
                ? brands.GetValueOrDefault(product.BrandId.Value)
                : null;

            decimal? discountedPrice = null;
            if (discountsByProduct.TryGetValue(product.Id, out var discount))
            {
                if (discount.Type == DiscountType.Percentage)
                    discountedPrice = product.BasePrice * (1 - discount.Value / 100m);
                else
                    discountedPrice = Math.Max(0, product.BasePrice - discount.Value);
            }

            result.Add(new CompareItemDto(
                item.Id,
                product.Id,
                product.Name,
                product.Slug,
                null,
                product.BasePrice,
                discountedPrice,
                brand?.Name,
                product.AvgRating,
                shop?.Name ?? string.Empty,
                shop?.Slug ?? string.Empty,
                product.ShopId,
                product.IsApproved,
                product.IsActive,
                totalStocks.GetValueOrDefault(product.Id, 0),
                item.CreatedAt));
        }

        if (itemsToRemove.Count > 0)
        {
            _context.CompareItems.RemoveRange(itemsToRemove);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success<IReadOnlyList<CompareItemDto>>(result);
    }
}