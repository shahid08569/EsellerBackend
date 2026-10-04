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
        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId && p.IsActive)
            .Select(p => new { p.Id, p.SourceProductId })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            return Result.Failure<IReadOnlyList<ProductImageDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        var images = await LoadImagesAsync(product.Id, cancellationToken);

        // Seller listing without own images → show master catalog images
        if (images.Count == 0 && product.SourceProductId.HasValue)
            images = await LoadImagesAsync(product.SourceProductId.Value, cancellationToken);

        return Result.Success<IReadOnlyList<ProductImageDto>>(images);
    }

    private async Task<List<ProductImageDto>> LoadImagesAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return await _context.ProductImages
            .Where(pi => pi.ProductId == productId)
            .OrderByDescending(pi => pi.IsCover)
            .ThenBy(pi => pi.SortOrder)
            .Select(pi => new ProductImageDto(
                pi.Id,
                pi.ProductId,
                pi.ImageUrl,
                pi.SortOrder,
                pi.IsCover))
            .ToListAsync(cancellationToken);
    }
}
