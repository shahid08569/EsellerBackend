using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductVariants.GetProductVariants;

public class GetProductVariantsQueryHandler
    : IRequestHandler<GetProductVariantsQuery, Result<IReadOnlyList<ProductVariantListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProductVariantsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<ProductVariantListDto>>> Handle(
        GetProductVariantsQuery request,
        CancellationToken cancellationToken)
    {
        // Verify product exists and is not deleted
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null,
                cancellationToken);

        if (product == null)
            return Result.Failure<IReadOnlyList<ProductVariantListDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        // Get variants with attributes
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => pv.ProductId == request.ProductId && pv.DeletedAt == null)
            .OrderBy(pv => pv.Sku)
            .ToListAsync(cancellationToken);

        // If product has no variants in DB, auto-provision a default variant so orders and catalog views never break
        if (variants.Count == 0)
        {
            try
            {
                var uniqueSuffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
                var cleanSlug = !string.IsNullOrWhiteSpace(product.Slug)
                    ? product.Slug.ToUpper()[..Math.Min(product.Slug.Length, 60)]
                    : $"PROD-{product.Id.ToString()[..8].ToUpper()}";
                var defaultSku = $"{cleanSlug}-{uniqueSuffix}-DEF";

                var defaultVariant = new Domain.Entities.Catalog.ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Sku = defaultSku,
                    Price = product.BasePrice > 0 ? product.BasePrice : 1,
                    StockQty = 100,
                    LowStockThreshold = 5,
                    IsActive = true
                };

                _context.ProductVariants.Add(defaultVariant);
                await _context.SaveChangesAsync(cancellationToken);
                variants.Add(defaultVariant);
            }
            catch (Exception)
            {
                // In case of race condition or collision, re-fetch existing variants
                variants = await _context.ProductVariants
                    .IgnoreQueryFilters()
                    .Where(pv => pv.ProductId == request.ProductId && pv.DeletedAt == null)
                    .OrderBy(pv => pv.Sku)
                    .ToListAsync(cancellationToken);
            }
        }

        var variantIds = variants.Select(v => v.Id).ToList();

        var attributes = await _context.VariantAttributeValues
            .Where(vav => variantIds.Contains(vav.ProductVariantId))
            .ToListAsync(cancellationToken);

        var attributesByVariant = attributes
            .GroupBy(a => a.ProductVariantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = variants.Select(v =>
        {
            var variantAttrs = attributesByVariant
                .GetValueOrDefault(v.Id, new List<Domain.Entities.Catalog.VariantAttributeValue>())
                .Select(a => new VariantAttributeDto(a.Id, a.AttributeName, a.AttributeValue))
                .ToList();

            return new ProductVariantListDto(
                v.Id,
                v.ProductId,
                v.Sku,
                v.Price,
                v.StockQty,
                v.LowStockThreshold,
                v.ImageUrl,
                v.IsActive,
                variantAttrs);
        }).ToList();

        return Result.Success<IReadOnlyList<ProductVariantListDto>>(result);
    }
}