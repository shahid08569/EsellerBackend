using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Wishlist.GetWishlist;

public class GetWishlistQueryHandler
    : IRequestHandler<GetWishlistQuery, Result<IReadOnlyList<WishlistItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetWishlistQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<IReadOnlyList<WishlistItemDto>>> Handle(
        GetWishlistQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<WishlistItemDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<IReadOnlyList<WishlistItemDto>>(
                "Only customers can access wishlist.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<IReadOnlyList<WishlistItemDto>>(
                "User not found.", "USER_NOT_FOUND");

        // 3. Get wishlist items
        var items = await _context.Wishlists
            .Where(w => w.UserId == user.Id)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return Result.Success<IReadOnlyList<WishlistItemDto>>(new List<WishlistItemDto>());

        var productIds = items.Select(w => w.ProductId).ToList();

        // 4. Get products
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var shopIds = products.Values.Select(p => p.ShopId).Distinct().ToList();

        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        // 5. Get active discounts for products
        var now = _dateTime.UtcNow;
        var activeDiscounts = await _context.Discounts
            .Where(d => d.IsActive
                     && d.ProductId.HasValue
                     && productIds.Contains(d.ProductId.Value)
                     && d.StartDate <= now
                     && d.EndDate >= now)
            .ToListAsync(cancellationToken);

        var discountsByProduct = activeDiscounts
            .Where(d => d.ProductId.HasValue)
            .GroupBy(d => d.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // 6. Build DTOs (excluding deleted products)
        var wishlistItems = new List<WishlistItemDto>();
        var itemsToRemove = new List<Domain.Entities.Engagement.Wishlist>();

        foreach (var item in items)
        {
            var product = products.GetValueOrDefault(item.ProductId);

            // Skip if product deleted
            if (product is null || product.DeletedAt != null)
            {
                itemsToRemove.Add(item);
                continue;
            }

            var shop = shops.GetValueOrDefault(product.ShopId);

            // Calculate discounted price
            decimal? discountedPrice = null;
            if (discountsByProduct.TryGetValue(product.Id, out var discount))
            {
                if (discount.Type == DiscountType.Percentage)
                    discountedPrice = product.BasePrice * (1 - discount.Value / 100m);
                else
                    discountedPrice = Math.Max(0, product.BasePrice - discount.Value);
            }

            wishlistItems.Add(new WishlistItemDto(
                item.Id,
                product.Id,
                product.Name,
                product.Slug,
                null,   // ProductImage — baad mein add karenge
                product.BasePrice,
                discountedPrice,
                product.AvgRating,
                shop?.Name ?? string.Empty,
                shop?.Slug ?? string.Empty,
                product.ShopId,
                product.IsApproved,
                product.IsActive,
                item.CreatedAt));
        }

        // 7. Auto-cleanup deleted products
        if (itemsToRemove.Count > 0)
        {
            _context.Wishlists.RemoveRange(itemsToRemove);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success<IReadOnlyList<WishlistItemDto>>(wishlistItems);
    }
}