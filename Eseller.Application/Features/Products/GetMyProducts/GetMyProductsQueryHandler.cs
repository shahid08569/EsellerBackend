using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.GetMyProducts;

public class GetMyProductsQueryHandler
    : IRequestHandler<GetMyProductsQuery, Result<PagedList<ProductListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyProductsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ProductListDto>>> Handle(
        GetMyProductsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ProductListDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // Verify shop ownership
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure<PagedList<ProductListDto>>(
                "Shop not found.", "SHOP_NOT_FOUND");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
            return Result.Failure<PagedList<ProductListDto>>(
                "You do not have permission.", "FORBIDDEN");

        // Include unapproved + rejected (shopkeeper apne saare products dekhe)
        var query = _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ShopId == request.ShopId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                _context.ProductImages
                    .Where(pi => pi.ProductId == p.Id)
                    .OrderBy(pi => pi.SortOrder)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault(),
                shop.Name,
                shop.Slug,
                _context.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                p.BrandId == null
                    ? null
                    : _context.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.IsApproved
                    ? "Approved"
                    : (p.RejectionReason == null ? "Pending" : "Rejected"),
                p.RejectionReason,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProductListDto>(
            products,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}