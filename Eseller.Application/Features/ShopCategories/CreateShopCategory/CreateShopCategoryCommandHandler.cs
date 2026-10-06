using System.Text.RegularExpressions;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ShopCategories.CreateShopCategory;

public class CreateShopCategoryCommandHandler
    : IRequestHandler<CreateShopCategoryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateShopCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        CreateShopCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var trimmedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            return Result.Failure<Guid>("Category name is required.", "NAME_REQUIRED");

        var slug = !string.IsNullOrWhiteSpace(request.Slug)
            ? Regex.Replace(request.Slug.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-')
            : Regex.Replace(trimmedName.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

        var exists = await _context.ShopCategories
            .AnyAsync(sc => sc.DeletedAt == null && (sc.Name == trimmedName || sc.Slug == slug), cancellationToken);

        if (exists)
            return Result.Failure<Guid>("A shop category with this name or slug already exists.", "CATEGORY_EXISTS");

        if (request.IsRecommended)
        {
            var others = await _context.ShopCategories
                .Where(sc => sc.DeletedAt == null && sc.IsRecommended)
                .ToListAsync(cancellationToken);
            foreach (var o in others)
                o.IsRecommended = false;
        }

        var shopCategory = new ShopCategory
        {
            Name = trimmedName,
            Slug = slug,
            Description = request.Description?.Trim(),
            BadgeText = request.BadgeText?.Trim() ?? $"{trimmedName} Store",
            BadgeColor = request.BadgeColor?.Trim() ?? "#F59E0B",
            IconUrl = request.IconUrl?.Trim() ?? "👑",
            DisplayOrder = request.DisplayOrder,
            PriceUsd = request.PriceUsd < 0 ? 0 : request.PriceUsd,
            MaxProductListings = request.MaxProductListings <= 0 ? 200 : request.MaxProductListings,
            IsRecommended = request.IsRecommended,
            IsActive = true
        };

        _context.ShopCategories.Add(shopCategory);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(shopCategory.Id);
    }
}
