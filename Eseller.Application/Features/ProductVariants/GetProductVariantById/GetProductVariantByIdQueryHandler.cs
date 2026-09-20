using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductVariants.GetProductVariantById;

public class GetProductVariantByIdQueryHandler
    : IRequestHandler<GetProductVariantByIdQuery, Result<ProductVariantDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductVariantByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductVariantDto>> Handle(
        GetProductVariantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(pv => pv.Id == request.VariantId
                                    && pv.ProductId == request.ProductId
                                    && pv.IsActive,
                cancellationToken);

        if (variant is null)
            return Result.Failure<ProductVariantDto>("Variant not found.", "VARIANT_NOT_FOUND");

        var attributes = await _context.VariantAttributeValues
            .Where(vav => vav.ProductVariantId == variant.Id)
            .Select(vav => new VariantAttributeDto(
                vav.Id,
                vav.AttributeName,
                vav.AttributeValue))
            .ToListAsync(cancellationToken);

        var dto = new ProductVariantDto(
            variant.Id,
            variant.ProductId,
            variant.Sku,
            variant.Price,
            variant.StockQty,
            variant.LowStockThreshold,
            variant.ImageUrl,
            variant.IsActive,
            attributes,
            variant.CreatedAt);

        return Result.Success(dto);
    }
}