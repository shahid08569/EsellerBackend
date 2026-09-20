using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Discounts.DeleteDiscount;

public class DeleteDiscountCommandHandler
    : IRequestHandler<DeleteDiscountCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteDiscountCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteDiscountCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var discount = await _context.Discounts
            .FirstOrDefaultAsync(d => d.Id == request.DiscountId, cancellationToken);

        if (discount is null)
            return Result.Failure("Discount not found.", "DISCOUNT_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            if (discount.ScopeType == DiscountScopeType.Platform
                || discount.ScopeType == DiscountScopeType.Category)
                return Result.Failure("Only admins can delete this discount.", "FORBIDDEN");

            if (discount.ScopeType == DiscountScopeType.Product && discount.ProductId.HasValue)
            {
                var product = await _context.Products
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == discount.ProductId, cancellationToken);

                var shop = product is null ? null : await _context.Shops
                    .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

                var shopkeeper = shop is null ? null : await _context.Shopkeepers
                    .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

                if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                    return Result.Failure("You do not have permission.", "FORBIDDEN");
            }
            else if (discount.ScopeType == DiscountScopeType.Shop && discount.ScopeId.HasValue)
            {
                var shop = await _context.Shops
                    .FirstOrDefaultAsync(s => s.Id == discount.ScopeId, cancellationToken);

                var shopkeeper = shop is null ? null : await _context.Shopkeepers
                    .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

                if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                    return Result.Failure("You do not have permission.", "FORBIDDEN");
            }
        }

        // Soft delete (IsActive = false)
        discount.IsActive = false;
        discount.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}