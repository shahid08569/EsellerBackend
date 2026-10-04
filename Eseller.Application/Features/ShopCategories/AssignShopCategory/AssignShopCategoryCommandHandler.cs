using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ShopCategories.AssignShopCategory;

public class AssignShopCategoryCommandHandler
    : IRequestHandler<AssignShopCategoryCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AssignShopCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        AssignShopCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId && s.DeletedAt == null, cancellationToken);

        if (shop == null)
            return Result.Failure("Shop not found.", "SHOP_NOT_FOUND");

        if (request.ShopCategoryId.HasValue)
        {
            var categoryExists = await _context.ShopCategories
                .AnyAsync(sc => sc.Id == request.ShopCategoryId.Value && sc.DeletedAt == null, cancellationToken);

            if (!categoryExists)
                return Result.Failure("Shop category not found.", "CATEGORY_NOT_FOUND");
        }

        shop.ShopCategoryId = request.ShopCategoryId;
        shop.CustomBadgeText = request.CustomBadgeText?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
