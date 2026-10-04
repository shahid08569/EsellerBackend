using System.Text.RegularExpressions;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ShopCategories.UpdateShopCategory;

public class UpdateShopCategoryCommandHandler
    : IRequestHandler<UpdateShopCategoryCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateShopCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        UpdateShopCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _context.ShopCategories
            .FirstOrDefaultAsync(sc => sc.Id == request.Id && sc.DeletedAt == null, cancellationToken);

        if (category == null)
            return Result.Failure("Shop category not found.", "CATEGORY_NOT_FOUND");

        var trimmedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            return Result.Failure("Category name is required.", "NAME_REQUIRED");

        var slug = !string.IsNullOrWhiteSpace(request.Slug)
            ? Regex.Replace(request.Slug.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-')
            : Regex.Replace(trimmedName.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

        var conflict = await _context.ShopCategories
            .AnyAsync(sc => sc.Id != request.Id && sc.DeletedAt == null && (sc.Name == trimmedName || sc.Slug == slug), cancellationToken);

        if (conflict)
            return Result.Failure("Another shop category with this name or slug already exists.", "CATEGORY_EXISTS");

        category.Name = trimmedName;
        category.Slug = slug;
        category.Description = request.Description?.Trim();
        category.BadgeText = request.BadgeText?.Trim();
        category.BadgeColor = request.BadgeColor?.Trim();
        category.IconUrl = request.IconUrl?.Trim();
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.PriceUsd = request.PriceUsd < 0 ? 0 : request.PriceUsd;
        category.MaxProductListings = request.MaxProductListings <= 0 ? 200 : request.MaxProductListings;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
