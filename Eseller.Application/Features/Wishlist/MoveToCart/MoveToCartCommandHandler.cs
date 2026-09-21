using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CartEntity = Eseller.Domain.Entities.Orders.Cart;
using CartItemEntity = Eseller.Domain.Entities.Orders.CartItem;

namespace Eseller.Application.Features.Wishlist.MoveToCart;

public class MoveToCartCommandHandler
    : IRequestHandler<MoveToCartCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public MoveToCartCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        MoveToCartCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure("Only customers can do this.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure("User not found.", "USER_NOT_FOUND");

        // 3. Verify wishlist item exists
        var wishlistItem = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == request.ProductId,
                cancellationToken);

        if (wishlistItem is null)
            return Result.Failure("Product not in wishlist.", "NOT_IN_WISHLIST");

        // 4. Verify variant
        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.ProductVariantId
                                    && pv.ProductId == request.ProductId
                                    && pv.IsActive
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure("Variant not available.", "VARIANT_NOT_AVAILABLE");

        // 5. Verify product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // 6. Stock check
        if (variant.StockQty <= 0)
            return Result.Failure("Product is out of stock.", "OUT_OF_STOCK");

        if (request.Quantity > variant.StockQty)
            return Result.Failure(
                $"Only {variant.StockQty} item(s) available.",
                "INSUFFICIENT_STOCK");

        // 7. Get or create cart
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
        {
            cart = new CartEntity { UserId = user.Id };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 8. Check existing cart item
        var existingCartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id
                                    && ci.ProductVariantId == request.ProductVariantId,
                cancellationToken);

        int newQuantity = existingCartItem is not null
            ? existingCartItem.Quantity + request.Quantity
            : request.Quantity;

        if (newQuantity > variant.StockQty)
            return Result.Failure(
                $"Only {variant.StockQty} item(s) available in stock.",
                "INSUFFICIENT_STOCK");

        // 9. Add or update cart item
        if (existingCartItem is not null)
        {
            existingCartItem.Quantity = newQuantity;
        }
        else
        {
            _context.CartItems.Add(new CartItemEntity
            {
                CartId = cart.Id,
                ProductVariantId = request.ProductVariantId,
                Quantity = newQuantity,
                CreatedAt = _dateTime.UtcNow
            });
        }

        // 10. Remove from wishlist
        _context.Wishlists.Remove(wishlistItem);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}