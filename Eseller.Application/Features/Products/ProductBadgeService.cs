using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products;

public interface IProductBadgeService
{
    Task<Dictionary<Guid, ProductBadgeDto>> CalculateBadgesAsync(
        List<Guid> productIds,
        CancellationToken cancellationToken);
}

public class ProductBadgeService : IProductBadgeService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public ProductBadgeService(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Dictionary<Guid, ProductBadgeDto>> CalculateBadgesAsync(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ProductBadgeDto>();

        var now = _dateTime.UtcNow;
        var newThreshold = now.AddDays(-7);
        var hotThreshold = now.AddDays(-30);

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.CreatedAt,
                p.BasePrice,
                p.IsFeatured,
                p.IsHotSelling,
                p.SourceProductId
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Catalog IDs linked to these listings (for flash / discount remap)
        var catalogKeys = products.Values
            .Select(p => p.SourceProductId ?? p.Id)
            .Distinct()
            .ToList();

        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(v => productIds.Contains(v.ProductId)
                     && v.IsActive
                     && v.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var stockByProduct = variants
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.StockQty));

        var bestSellingIds = await _context.OrderRequestItems
            .Join(_context.OrderRequests,
                oi => oi.OrderRequestId,
                o => o.Id,
                (oi, o) => new { oi, o })
            .Join(_context.ProductVariants,
                x => x.oi.ProductVariantId,
                pv => pv.Id,
                (x, pv) => new { x.oi, x.o, pv })
            .Where(x => x.o.Status == OrderStatus.Delivered)
            .GroupBy(x => x.pv.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalSold = g.Sum(x => x.oi.Quantity)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(10)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        var hotSellingSalesIds = await _context.OrderRequestItems
            .Join(_context.OrderRequests,
                oi => oi.OrderRequestId,
                o => o.Id,
                (oi, o) => new { oi, o })
            .Join(_context.ProductVariants,
                x => x.oi.ProductVariantId,
                pv => pv.Id,
                (x, pv) => new { x.oi, x.o, pv })
            .Where(x => x.o.Status == OrderStatus.Delivered
                     && x.o.CreatedAt >= hotThreshold)
            .GroupBy(x => x.pv.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalSold = g.Sum(x => x.oi.Quantity)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(10)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        // Flash keyed by catalog ProductId — also match listings via SourceProductId
        var flashRows = await _context.FlashSaleProducts
            .Join(_context.FlashSales,
                fsp => fsp.FlashSaleId,
                fs => fs.Id,
                (fsp, fs) => new { fsp, fs })
            .Where(x => x.fs.IsActive
                     && x.fs.StartDate <= now
                     && x.fs.EndDate >= now
                     && (catalogKeys.Contains(x.fsp.ProductId)
                         || productIds.Contains(x.fsp.ProductId)))
            .Select(x => new
            {
                x.fsp.ProductId,
                x.fsp.DiscountType,
                x.fsp.DiscountValue,
                x.fs.EndDate
            })
            .ToListAsync(cancellationToken);

        var flashSaleDict = flashRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.EndDate).First());

        var discountLookupIds = catalogKeys.Concat(productIds).Distinct().ToList();
        var discounts = await _context.Discounts
            .Where(d => d.ProductId != null
                     && discountLookupIds.Contains(d.ProductId.Value)
                     && d.IsActive
                     && d.StartDate <= now
                     && d.EndDate >= now)
            .ToListAsync(cancellationToken);

        var discountByProduct = discounts
            .Where(d => d.ProductId.HasValue)
            .GroupBy(d => d.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new Dictionary<Guid, ProductBadgeDto>();

        foreach (var productId in productIds)
        {
            var product = products.GetValueOrDefault(productId);
            if (product is null) continue;

            var catalogKey = product.SourceProductId ?? product.Id;
            var stock = stockByProduct.GetValueOrDefault(productId, 0);

            var flashSale = flashSaleDict.GetValueOrDefault(productId)
                ?? flashSaleDict.GetValueOrDefault(catalogKey);

            var discount = discountByProduct.GetValueOrDefault(productId)
                ?? discountByProduct.GetValueOrDefault(catalogKey);

            decimal? discountPercent = null;
            if (discount is not null && product.BasePrice > 0)
            {
                discountPercent = discount.Type == DiscountType.Percentage
                    ? discount.Value
                    : (discount.Value / product.BasePrice) * 100m;
            }

            decimal? flashSaleDiscountPercent = null;
            if (flashSale is not null && product.BasePrice > 0)
            {
                flashSaleDiscountPercent = flashSale.DiscountType == DiscountType.Percentage
                    ? flashSale.DiscountValue
                    : (flashSale.DiscountValue / product.BasePrice) * 100m;
            }

            var isHot = product.IsHotSelling
                        || hotSellingSalesIds.Contains(productId)
                        || (product.SourceProductId.HasValue
                            && hotSellingSalesIds.Contains(product.SourceProductId.Value));

            result[productId] = new ProductBadgeDto(
                IsNew: product.CreatedAt >= newThreshold,
                IsBestSelling: bestSellingIds.Contains(productId)
                    || (product.SourceProductId.HasValue && bestSellingIds.Contains(product.SourceProductId.Value)),
                IsHotSelling: isHot,
                IsFeatured: product.IsFeatured,
                IsFlashSale: flashSale is not null,
                IsLowStock: stock > 0 && stock < 10,
                IsOutOfStock: stock == 0,
                DiscountPercent: discountPercent.HasValue
                    ? Math.Round(discountPercent.Value, 0)
                    : null,
                FlashSaleDiscountPercent: flashSaleDiscountPercent.HasValue
                    ? Math.Round(flashSaleDiscountPercent.Value, 0)
                    : null,
                FlashSaleEndDate: flashSale?.EndDate);
        }

        return result;
    }
}
