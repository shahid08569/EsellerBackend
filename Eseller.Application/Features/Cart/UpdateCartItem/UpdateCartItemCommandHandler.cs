using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cart.UpdateCartItem;

public class UpdateCartItemCommandHandler
    : IRequestHandler<UpdateCartItemCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCartItemCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        UpdateCartItemCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure("Only customers can update cart.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure("User not found.", "USER_NOT_FOUND");

        // 3. Get cart
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
            return Result.Failure("Cart not found.", "CART_NOT_FOUND");

        // 4. Get cart item
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id
                                    && ci.ProductVariantId == request.ProductVariantId,
                cancellationToken);

        if (cartItem is null)
            return Result.Failure("Cart item not found.", "CART_ITEM_NOT_FOUND");

        // 5. Verify variant is still valid
        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.ProductVariantId
                                    && pv.IsActive
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure("Variant is no longer available.", "VARIANT_NOT_AVAILABLE");

        // 6. Stock check
        if (request.Quantity > variant.StockQty)
            return Result.Failure(
                $"Only {variant.StockQty} item(s) available in stock.",
                "INSUFFICIENT_STOCK");

        // 7. Update quantity
        cartItem.Quantity = request.Quantity;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}