using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Compare.GetCompareDetails;

public class GetCompareDetailsQueryHandler
    : IRequestHandler<GetCompareDetailsQuery, Result<CompareDetailsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetCompareDetailsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<CompareDetailsDto>> Handle(
        GetCompareDetailsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<CompareDetailsDto>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<CompareDetailsDto>("Only customers can compare.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<CompareDetailsDto>("User not found.", "USER_NOT_FOUND");

        var items = await _context.CompareItems
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return Result.Success(new CompareDetailsDto(
                new List<CompareProductDetails>(),
                new List<CompareAttributeGroup>()));

        var productIds = items.Select(c => c.ProductId).ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var categoryIds = products.Values.Select(p => p.CategoryId).Distinct().ToList();
        var categories = await _context.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var brandIds = products.Values
            .Where(p => p.BrandId.HasValue)
            .Select(p => p.BrandId!.Value)
            .Distinct()
            .ToList();

        var brands = await _context.Brands
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        var shopIds = products.Values.Select(p => p.ShopId).Distinct().ToList();
        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        // Variants with attributes
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => productIds.Contains(pv.ProductId)
                      && pv.IsActive
                      && pv.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var variantIds = variants.Select(v => v.Id).ToList();

        var variantAttributes = await _context.VariantAttributeValues
            .Where(vav => variantIds.Contains(vav.ProductVariantId))
            .ToListAsync(cancellationToken);

        var attributesByVariant = variantAttributes
            .GroupBy(a => a.ProductVariantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build product details
        var productDetails = new List<CompareProductDetails>();

        foreach (var item in items)
        {
            var product = products.GetValueOrDefault(item.ProductId);
            if (product is null) continue;

            var category = categories.GetValueOrDefault(product.CategoryId);
            var brand = product.BrandId.HasValue
                ? brands.GetValueOrDefault(product.BrandId.Value)
                : null;
            var shop = shops.GetValueOrDefault(product.ShopId);

            var productVariants = variants
                .Where(v => v.ProductId == product.Id)
                .Select(v => new CompareVariantSummary(
                    v.Id,
                    v.Sku,
                    v.Price,
                    v.StockQty,
                    attributesByVariant
                        .GetValueOrDefault(v.Id, new List<Domain.Entities.Catalog.VariantAttributeValue>())
                        .Select(a => $"{a.AttributeName}: {a.AttributeValue}")
                        .ToList()))
                .ToList();

            var totalStock = productVariants.Sum(v => v.StockQty);

            productDetails.Add(new CompareProductDetails(
                product.Id,
                product.Name,
                product.Slug,
                null,
                product.BasePrice,
                null,
                brand?.Name,
                category?.Name,
                product.AvgRating,
                shop?.Name ?? string.Empty,
                shop?.Slug ?? string.Empty,
                totalStock,
                totalStock > 0,
                productVariants));
        }

        // Build attribute groups (side-by-side)
        var allAttributeNames = variantAttributes
            .Select(a => a.AttributeName)
            .Distinct()
            .ToList();

        var attributeGroups = new List<CompareAttributeGroup>();

        foreach (var attrName in allAttributeNames)
        {
            var values = new List<string?>();
            foreach (var item in items)
            {
                var product = products.GetValueOrDefault(item.ProductId);
                if (product is null) { values.Add(null); continue; }

                var productVariantIds = variants
                    .Where(v => v.ProductId == product.Id)
                    .Select(v => v.Id)
                    .ToList();

                var value = variantAttributes
                    .Where(a => a.AttributeName == attrName
                             && productVariantIds.Contains(a.ProductVariantId))
                    .Select(a => a.AttributeValue)
                    .Distinct()
                    .ToList();

                values.Add(value.Count > 0 ? string.Join(", ", value) : null);
            }

            attributeGroups.Add(new CompareAttributeGroup(attrName, values));
        }

        return Result.Success(new CompareDetailsDto(productDetails, attributeGroups));
    }
}