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
        // Verify product exists and is active
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == request.ProductId && p.IsActive && p.IsApproved,
                cancellationToken);

        if (!productExists)
            return Result.Failure<IReadOnlyList<ProductVariantListDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        // Get variants with attributes
        var variants = await _context.ProductVariants
            .Where(pv => pv.ProductId == request.ProductId && pv.IsActive && pv.DeletedAt==null)
            .OrderBy(pv => pv.Sku)
            .ToListAsync(cancellationToken);

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