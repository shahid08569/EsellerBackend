using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Admin.Shopkeepers.ApproveShopkeeper;

public class ApproveShopkeeperCommandHandler
    : IRequestHandler<ApproveShopkeeperCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApproveShopkeeperCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        ApproveShopkeeperCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(s => s.Id == request.ShopkeeperId, cancellationToken);

        if (shopkeeper is null)
            return Result.Failure("Shopkeeper not found.", "SHOPKEEPER_NOT_FOUND");

        if (_currentUserService.RoleType == RoleType.Partner)
        {
            var partner = await _context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == _currentUserService.AccountId.Value, cancellationToken);
            if (partner == null || shopkeeper.PartnerId != partner.Id)
            {
                return Result.Failure("Partners can only approve merchants that registered through their own referral link.", "FORBIDDEN");
            }
        }

        if (shopkeeper.Status != ShopkeeperApprovalStatus.Pending)
            return Result.Failure(
                $"Shopkeeper is already {shopkeeper.Status}.",
                "INVALID_STATUS");

        shopkeeper.Status = ShopkeeperApprovalStatus.Approved;
        shopkeeper.ApprovedByAccountId = _currentUserService.AccountId;
        shopkeeper.ApprovedAt = _dateTime.UtcNow;
        shopkeeper.SetUpdatedAt(_dateTime.UtcNow);

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id, cancellationToken);

        if (shop is not null)
        {
            shop.IsActive = true;
            shop.SetUpdatedAt(_dateTime.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
