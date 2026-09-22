using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.ProductVariants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cart.GetCart;

public class GetCartQueryHandler
    : IRequestHandler<GetCartQuery, Result<CartDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCartQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CartDto>> Handle(
        GetCartQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<CartDto>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != Domain.Enums.RoleType.User)
            return Result.Failure<CartDto>("Only customers can access cart.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<CartDto>("User not found.", "USER_NOT_FOUND");

        // 3. Get cart
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
        {
            // Empty cart
            return Result.Success(new CartDto(Guid.Empty, new List<CartItemDto>(), 0, 0m));
        }

        // 4. Get cart items with details
        var items = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Result.Success(new CartDto(cart.Id, new List<CartItemDto>(), 0, 0m));
        }

        var variantIds = items.Select(ci => ci.ProductVariantId).ToList();

        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id))
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var shopIds = products.Values.Select(p => p.ShopId).Distinct().ToList();

        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        // 5. Get variant attributes
        var attributes = await _context.VariantAttributeValues
            .Where(vav => variantIds.Contains(vav.ProductVariantId))
            .ToListAsync(cancellationToken);

        var attributesByVariant = attributes
            .GroupBy(a => a.ProductVariantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6. Build DTOs — filter out inactive/deleted variants
        var cartItems = new List<CartItemDto>();
        var itemsToRemove = new List<Domain.Entities.Orders.CartItem>();

        foreach (var item in items)
        {
            var variant = variants.GetValueOrDefault(item.ProductVariantId);
            var product = variant is null ? null : products.GetValueOrDefault(variant.ProductId);
            var shop = product is null ? null : shops.GetValueOrDefault(product.ShopId);

            // Skip if variant/product/shop is inactive or deleted
            if (variant is null || !variant.IsActive || variant.DeletedAt != null
                || product is null || !product.IsActive || product.DeletedAt != null
                || shop is null || !shop.IsActive)
            {
                itemsToRemove.Add(item);
                continue;
            }

            var variantAttrs = attributesByVariant
                .GetValueOrDefault(variant.Id, new List<Domain.Entities.Catalog.VariantAttributeValue>())
                .Select(a => new VariantAttributeDto(a.Id, a.AttributeName, a.AttributeValue))
                .ToList();

            cartItems.Add(new CartItemDto(
                item.Id,
                product.Id,
                variant.Id,
                product.Name,
                product.Slug,
                variant.Sku,
                variant.ImageUrl,
                null,   // ProductImage — optional, baad mein add karenge
                variant.Price,
                item.Quantity,
                variant.StockQty,
                variant.Price * item.Quantity,
                shop.Name,
                shop.Slug,
                shop.Id,
                variantAttrs));
        }

        // 7. Remove invalid items from cart (auto-cleanup)
        if (itemsToRemove.Count > 0)
        {
            _context.CartItems.RemoveRange(itemsToRemove);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var totalItems = cartItems.Sum(i => i.Quantity);
        var subTotal = cartItems.Sum(i => i.TotalPrice);

        return Result.Success(new CartDto(cart.Id, cartItems, totalItems, subTotal));
    }
}