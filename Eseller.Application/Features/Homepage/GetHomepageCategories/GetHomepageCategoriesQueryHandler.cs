using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Homepage.GetHomepageCategories;

public class GetHomepageCategoriesQueryHandler
    : IRequestHandler<GetHomepageCategoriesQuery, Result<List<HomepageCategorySectionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductBadgeService _badgeService;

    public GetHomepageCategoriesQueryHandler(
        IApplicationDbContext context,
        IProductBadgeService badgeService)
    {
        _context = context;
        _badgeService = badgeService;
    }

    public async Task<Result<List<HomepageCategorySectionDto>>> Handle(
        GetHomepageCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit > 20 ? 20 : (request.Limit < 1 ? 11 : request.Limit);
        var productsPerCategory = request.ProductsPerCategory > 20 ? 20 : (request.ProductsPerCategory < 1 ? 8 : request.ProductsPerCategory);

        // ? 1. Featured categories fetch karo
        var categories = await _context.Categories
            .Where(c => c.IsFeaturedOnHomepage && c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.HomepageDisplayOrder)
            .ThenBy(c => c.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0)
            return Result.Success(new List<HomepageCategorySectionDto>());

        var categoryIds = categories.Select(c => c.Id).ToList();

        // ? 2. Har category ke products fetch karo (seller storefront only)
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);
        var productsByCategory = new Dictionary<Guid, List<Guid>>();

        foreach (var categoryId in categoryIds)
        {
            var productIds = await MasterCatalog.StorefrontListings(_context, masterShopId)
                .Where(p => p.CategoryId == categoryId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(productsPerCategory)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            productsByCategory[categoryId] = productIds;
        }

        // ? 3. Saare products ek saath load karo (efficient)
        var allProductIds = productsByCategory.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToList();

        var productDtos = await LoadProducts(allProductIds, cancellationToken);

        // ? 4. Category sections build karo (order preserve)
        var result = categories.Select(c =>
        {
            var productIds = productsByCategory.GetValueOrDefault(c.Id, new List<Guid>());

            var products = productIds
                .Where(id => productDtos.ContainsKey(id))
                .Select(id => productDtos[id])
                .ToList();

            return new HomepageCategorySectionDto(
                c.Id,
                c.Name,
                c.Slug,
                c.ImageUrl,
                c.HomepageDisplayOrder,
                products);
        }).ToList();

        return Result.Success(result);
    }

    private async Task<Dictionary<Guid, ProductListDto>> LoadProducts(
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ProductListDto>();

        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.Description,
                p.SourceProductId,
                PrimaryImageUrl = _context.ProductImages
                    .Where(pi => pi.ProductId == p.Id)
                    .OrderByDescending(pi => pi.IsCover)
                    .ThenBy(pi => pi.SortOrder)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault()
                    ?? (p.SourceProductId == null
                        ? null
                        : _context.ProductImages
                            .Where(pi => pi.ProductId == p.SourceProductId)
                            .OrderByDescending(pi => pi.IsCover)
                            .ThenBy(pi => pi.SortOrder)
                            .Select(pi => pi.ImageUrl)
                            .FirstOrDefault()),
                ShopName = _context.Shops
                    .Where(s => s.Id == p.ShopId).Select(s => s.Name)
                    .FirstOrDefault() ?? string.Empty,
                ShopSlug = _context.Shops
                    .Where(s => s.Id == p.ShopId).Select(s => s.Slug)
                    .FirstOrDefault() ?? string.Empty,
                ShopBadgeText = (
                    from s in _context.Shops
                    where s.Id == p.ShopId
                    join sc in _context.ShopCategories on s.ShopCategoryId equals sc.Id into scj
                    from sc in scj.DefaultIfEmpty()
                    select s.CustomBadgeText ?? sc.BadgeText ?? sc.Name ?? "Bronze"
                ).FirstOrDefault(),
                ShopBadgeColor = (
                    from s in _context.Shops
                    where s.Id == p.ShopId
                    join sc in _context.ShopCategories on s.ShopCategoryId equals sc.Id into scj
                    from sc in scj.DefaultIfEmpty()
                    select sc.BadgeColor ?? "#94A3B8"
                ).FirstOrDefault(),
                CategoryName = _context.Categories
                    .Where(c => c.Id == p.CategoryId).Select(c => c.Name)
                    .FirstOrDefault() ?? string.Empty,
                BrandName = p.BrandId == null
                    ? null
                    : _context.Brands
                        .Where(b => b.Id == p.BrandId).Select(b => b.Name)
                        .FirstOrDefault(),
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.RejectionReason,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var badges = await _badgeService.CalculateBadgesAsync(productIds, cancellationToken);

        return products.ToDictionary(
            p => p.Id,
            p => new ProductListDto(
                p.Id,
                p.Name,
                p.Slug,
                p.BasePrice,
                p.PrimaryImageUrl,
                p.ShopName,
                p.ShopSlug,
                p.CategoryName,
                p.BrandName,
                p.AvgRating,
                p.IsFeatured,
                p.IsApproved,
                p.IsApproved ? "Approved" : (p.RejectionReason == null ? "Pending" : "Rejected"),
                p.RejectionReason,
                p.CreatedAt,
                badges.GetValueOrDefault(p.Id),
                ShopBadgeText: p.ShopBadgeText,
                ShopBadgeColor: p.ShopBadgeColor,
                Description: TruncateDesc(p.Description)));
    }

    private static string? TruncateDesc(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var clean = description.Replace("\r", " ").Replace("\n", " ").Trim();
        while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
        return clean.Length <= 120 ? clean : clean[..117].TrimEnd() + "…";
    }
}