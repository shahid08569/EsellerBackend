using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.DeleteShop;

public class DeleteShopCommandHandler
    : IRequestHandler<DeleteShopCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteShopCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteShopCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        // 2. Find shop
        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure("Shop not found.", "SHOP_NOT_FOUND");

        // 3. Data Isolation — verify ownership
        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        var isSuperAdmin = _currentUserService.RoleType == RoleType.SuperAdmin || _currentUserService.RoleType == RoleType.Partner;
        if (!isSuperAdmin && (shopkeeper == null || shopkeeper.AccountId != _currentUserService.AccountId.Value))
            return Result.Failure(
                "You do not have permission to delete this shop.",
                "FORBIDDEN");

        // 4. Soft delete shop
        shop.IsActive = false;
        shop.DeletedAt = _dateTime.UtcNow;
        shop.SetUpdatedAt(_dateTime.UtcNow);

        // Also soft-delete any shop-specific products
        var shopProducts = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.ShopId == shop.Id && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var p in shopProducts)
        {
            p.DeletedAt = _dateTime.UtcNow;
        }

        // 5. SuperAdmin/Partner delete: remove seller from applications list too
        if (isSuperAdmin && shopkeeper is not null)
        {
            shopkeeper.Status = ShopkeeperApprovalStatus.Rejected;
            shopkeeper.RejectionReason = string.IsNullOrWhiteSpace(shopkeeper.RejectionReason)
                ? "Store deleted by Super Admin."
                : shopkeeper.RejectionReason;
            shopkeeper.SetUpdatedAt(_dateTime.UtcNow);

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == shopkeeper.AccountId, cancellationToken);
            if (account is not null)
            {
                account.IsActive = false;
                account.SetUpdatedAt(_dateTime.UtcNow);
            }
        }

        // 6. Save
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}