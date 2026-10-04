using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using Eseller.Application.Features.Shops.GetShopById;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.GetShopBySlug;

public class GetShopBySlugQueryHandler
    : IRequestHandler<GetShopBySlugQuery, Result<ShopDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShopBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ShopDetailDto>> Handle(
        GetShopBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Slug == request.Slug && s.IsActive, cancellationToken);

        if (shop is null)
            return Result.Failure<ShopDetailDto>("Shop not found.", "SHOP_NOT_FOUND");

        // Master warehouse is admin-only — never expose on customer site
        if (shop.Slug == MasterCatalog.ShopSlug || shop.Name == MasterCatalog.ShopName)
            return Result.Failure<ShopDetailDto>("Shop not found.", "SHOP_NOT_FOUND");

        var listedCount = await MasterCatalog.StorefrontListings(_context, masterShopId: null)
            .Where(p => p.ShopId == shop.Id)
            .CountAsync(cancellationToken);

        if (listedCount == 0)
            return Result.Failure<ShopDetailDto>("Shop not found.", "SHOP_NOT_FOUND");

        var dto = new ShopDetailDto(
            shop.Id,
            shop.Name,
            shop.Slug,
            shop.Description,
            shop.LogoUrl,
            shop.BannerUrl,
            shop.Phone,
            shop.Address,
            shop.City,
            shop.Country,
            shop.Rating,
            listedCount,
            shop.IsActive,
            shop.CreatedAt);

        return Result.Success(dto);
    }
}