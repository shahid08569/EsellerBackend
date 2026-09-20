using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Discounts.CreateDiscount;

public class CreateDiscountCommandHandler
    : IRequestHandler<CreateDiscountCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateDiscountCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateDiscountCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        // Permission checks based on scope
        if (request.ScopeType == DiscountScopeType.Product)
        {
            var product = await _context.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null,
                    cancellationToken);

            if (product is null)
                return Result.Failure<Guid>("Product not found.", "PRODUCT_NOT_FOUND");

            if (!isAdmin)
            {
                // Verify shop ownership
                var shop = await _context.Shops
                    .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

                var shopkeeper = shop is null ? null : await _context.Shopkeepers
                    .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

                if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                    return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
            }
        }
        else if (request.ScopeType == DiscountScopeType.Shop)
        {
            // Only admins can create shop-level discounts (ya shopkeeper apni shop ke liye)
            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == request.ScopeId, cancellationToken);

            if (shop is null)
                return Result.Failure<Guid>("Shop not found.", "SHOP_NOT_FOUND");

            if (!isAdmin)
            {
                var shopkeeper = await _context.Shopkeepers
                    .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

                if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                    return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
            }
        }
        else if (request.ScopeType == DiscountScopeType.Category)
        {
            // Only admins can create category-level discounts
            if (!isAdmin)
                return Result.Failure<Guid>("Only admins can create category discounts.", "FORBIDDEN");

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == request.ScopeId && c.IsActive, cancellationToken);

            if (!categoryExists)
                return Result.Failure<Guid>("Category not found.", "CATEGORY_NOT_FOUND");
        }
        else if (request.ScopeType == DiscountScopeType.Platform)
        {
            // Only admins can create platform-wide discounts
            if (!isAdmin)
                return Result.Failure<Guid>("Only admins can create platform discounts.", "FORBIDDEN");
        }

        // Time overlap check
        var overlappingDiscount = await _context.Discounts
            .AnyAsync(d => d.ScopeType == request.ScopeType
                        && d.ScopeId == request.ScopeId
                        && d.ProductId == request.ProductId
                        && d.IsActive
                        && d.StartDate < request.EndDate
                        && d.EndDate > request.StartDate,
                cancellationToken);

        if (overlappingDiscount)
            return Result.Failure<Guid>(
                "An overlapping discount already exists for this scope.",
                "DISCOUNT_OVERLAP");

        var discount = new Discount
        {
            ProductId = request.ProductId,
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            Type = request.Type,
            Value = request.Value,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true
        };

        _context.Discounts.Add(discount);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(discount.Id);
    }
}