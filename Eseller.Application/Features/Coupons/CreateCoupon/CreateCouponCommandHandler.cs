using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Promotions;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Coupons.CreateCoupon;

public class CreateCouponCommandHandler
    : IRequestHandler<CreateCouponCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateCouponCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(
        CreateCouponCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        // Shopkeepers may only create Restricted (product-scoped) coupons — never Platform-wide
        if (!isAdmin && request.ScopeType == CouponScopeType.Platform)
            return Result.Failure<Guid>(
                "Shopkeepers cannot create platform-wide coupons.",
                "FORBIDDEN");

        var code = request.Code.Trim().ToUpperInvariant();

        var codeExists = await _context.Coupons
            .AnyAsync(c => c.Code == code, cancellationToken);

        if (codeExists)
            return Result.Failure<Guid>("Coupon code already exists.", "CODE_TAKEN");

        var coupon = new Coupon
        {
            Code = code,
            Type = request.Type,
            Value = request.Value,
            MinOrderAmount = request.MinOrderAmount,
            MaxDiscount = request.MaxDiscount,
            ExpiryDate = request.ExpiryDate,
            UsageLimit = request.UsageLimit,
            UsedCount = 0,
            ScopeType = request.ScopeType,
            CreatedByAccountId = _currentUserService.AccountId.Value,
            IsActive = true
        };

        _context.Coupons.Add(coupon);

        if (request.ScopeType == CouponScopeType.Restricted
            && request.ProductIds?.Count > 0
            && !isAdmin)
        {
            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (shopkeeper is null)
                return Result.Failure<Guid>("Shopkeeper not found.", "SHOPKEEPER_NOT_FOUND");

            var ownedProductCount = await _context.Products
                .Where(p => request.ProductIds.Contains(p.Id) && p.ShopId != Guid.Empty)
                .Join(_context.Shops.Where(s => s.ShopkeeperId == shopkeeper.Id),
                      p => p.ShopId, s => s.Id, (p, s) => p)
                .CountAsync(cancellationToken);

            if (ownedProductCount != request.ProductIds.Count)
                return Result.Failure<Guid>(
                    "You do not have permission to create coupons for these products.",
                    "FORBIDDEN");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(coupon.Id);
    }
}
