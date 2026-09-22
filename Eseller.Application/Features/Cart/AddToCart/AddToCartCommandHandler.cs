using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cart.AddToCart;

public class AddToCartCommandHandler
    : IRequestHandler<AddToCartCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AddToCartCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        AddToCartCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<Guid>("Only customers can add to cart.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        // 3. Get variant
        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.ProductVariantId
                                    && pv.IsActive
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure<Guid>("Product variant not found or unavailable.",
                "VARIANT_NOT_FOUND");

        // 4. Get product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == variant.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // 5. Verify shop active
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == product.ShopId && s.IsActive, cancellationToken);

        if (shop is null)
            return Result.Failure<Guid>("Shop not available.", "SHOP_NOT_AVAILABLE");

        // 6. Stock check
        if (variant.StockQty <= 0)
            return Result.Failure<Guid>("Product is out of stock.", "OUT_OF_STOCK");

        // 7. Get or create cart
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
        {
            cart = new Eseller.Domain.Entities.Orders.Cart { UserId = user.Id };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 8. Check existing item
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id
                                    && ci.ProductVariantId == request.ProductVariantId,
                cancellationToken);

        int newQuantity;

        if (existingItem is not null)
        {
            newQuantity = existingItem.Quantity + request.Quantity;
        }
        else
        {
            newQuantity = request.Quantity;
        }

        // 9. Stock check for new quantity
        if (newQuantity > variant.StockQty)
            return Result.Failure<Guid>(
                $"Only {variant.StockQty} item(s) available in stock.",
                "INSUFFICIENT_STOCK");

        // 10. Add or update
        if (existingItem is not null)
        {
            existingItem.Quantity = newQuantity;
        }
        else
        {
            existingItem = new CartItem
            {
                CartId = cart.Id,
                ProductVariantId = request.ProductVariantId,
                Quantity = newQuantity,
                CreatedAt = _dateTime.UtcNow
            };
            _context.CartItems.Add(existingItem);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(existingItem.Id);
    }
}