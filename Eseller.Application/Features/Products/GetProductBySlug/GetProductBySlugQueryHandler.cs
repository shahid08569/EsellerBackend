using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetProductBySlug;

public class GetProductBySlugQueryHandler
    : IRequestHandler<GetProductBySlugQuery, Result<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductDto>> Handle(
        GetProductBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        var product = await _context.Products
            .Where(p => p.Slug == request.Slug && p.IsActive && p.IsApproved && p.DeletedAt == null)
            .Where(p =>
                !masterShopId.HasValue
                || p.ShopId != masterShopId.Value
                || (p.ShopId == masterShopId.Value && p.SourceProductId == null && p.IsStorefrontLive))
            .Select(p => new ProductDto(
                p.Id,
                p.ShopId,
                _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Slug).FirstOrDefault() ?? string.Empty,
                p.CategoryId,
                _context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                p.BrandId,
                p.BrandId == null
                    ? null
                    : _context.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                p.Name,
                p.Slug,
                p.Description,
                p.BasePrice,
                p.IsFeatured,
                p.IsApproved,
                p.IsApproved
                    ? "Approved"
                    : (p.RejectionReason == null ? "Pending" : "Rejected"),
                p.RejectionReason,
                p.ViewCount,
                p.AvgRating,
                p.SeoTitle,
                p.SeoDescription,
                p.IsActive,
                p.CreatedAt,
                _context.Shops.Where(s => s.Id == p.ShopId).Select(s => s.Rating).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            return Result.Failure<ProductDto>("Product not found.", "PRODUCT_NOT_FOUND");

        // Fire-and-forget view count — never block the product page on MonsterASP DB write.
        _ = IncrementViewCountAsync(product.Id);

        return Result.Success(product);
    }

    private async Task IncrementViewCountAsync(Guid productId)
    {
        try
        {
            var tracked = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);
            if (tracked is null) return;
            tracked.ViewCount++;
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // Non-critical metric
        }
    }
}