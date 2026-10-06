using Eseller.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products;

/// <summary>Resolves shop tier badge text/color for product cards.</summary>
public static class ShopBadgeProjection
{
    public static IQueryable<ShopBadgeInfo> ForShop(IApplicationDbContext context, Guid shopId) =>
        from s in context.Shops.AsNoTracking()
        where s.Id == shopId
        join sc in context.ShopCategories.AsNoTracking()
            on s.ShopCategoryId equals sc.Id into scj
        from sc in scj.DefaultIfEmpty()
        select new ShopBadgeInfo(
            s.CustomBadgeText ?? sc.BadgeText ?? sc.Name ?? "Bronze",
            sc.BadgeColor ?? "#94A3B8");

    public static string? ResolveText(string? customBadge, string? categoryBadge, string? categoryName) =>
        FirstNonEmpty(customBadge, categoryBadge, categoryName) ?? "Bronze";

    public static string ResolveColor(string? categoryColor) =>
        string.IsNullOrWhiteSpace(categoryColor) ? "#94A3B8" : categoryColor.Trim();

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }
}

public record ShopBadgeInfo(string? Text, string? Color);
