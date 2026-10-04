using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ShopCategories.DeleteShopCategory;

public class DeleteShopCategoryCommandHandler
    : IRequestHandler<DeleteShopCategoryCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeleteShopCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        DeleteShopCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _context.ShopCategories
            .FirstOrDefaultAsync(sc => sc.Id == request.Id && sc.DeletedAt == null, cancellationToken);

        if (category == null)
            return Result.Failure("Shop category not found.", "CATEGORY_NOT_FOUND");

        category.DeletedAt = DateTime.UtcNow;
        category.IsActive = false;

        // Unlink any shops assigned to this category
        var linkedShops = await _context.Shops
            .Where(s => s.ShopCategoryId == request.Id)
            .ToListAsync(cancellationToken);

        foreach (var shop in linkedShops)
        {
            shop.ShopCategoryId = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
