using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetProductImages;

public class GetProductImagesQueryHandler
    : IRequestHandler<GetProductImagesQuery, Result<IReadOnlyList<ProductImageDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProductImagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<ProductImageDto>>> Handle(
        GetProductImagesQuery request,
        CancellationToken cancellationToken)
    {
        // Verify product exists
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == request.ProductId && p.IsActive, cancellationToken);

        if (!productExists)
            return Result.Failure<IReadOnlyList<ProductImageDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        var images = await _context.ProductImages
            .Where(pi => pi.ProductId == request.ProductId)
            .OrderBy(pi => pi.SortOrder)
            .Select(pi => new ProductImageDto(
                pi.Id,
                pi.ProductId,
                pi.ImageUrl,
                pi.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductImageDto>>(images);
    }
}