using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.GetShopById;

public class GetShopByIdQueryHandler
    : IRequestHandler<GetShopByIdQuery, Result<ShopDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShopByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ShopDetailDto>> Handle(
        GetShopByIdQuery request,
        CancellationToken cancellationToken)
    {
        var shop = await _context.Shops
     .FirstOrDefaultAsync(s => s.Id == request.ShopId && s.IsActive, cancellationToken);
        
        if (shop is null)
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
            shop.TotalProducts,
            shop.IsActive,
            shop.CreatedAt);

        return Result.Success(dto);
    }
}