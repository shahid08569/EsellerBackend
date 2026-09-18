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
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        // 2. Find shopkeeper
        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(s => s.Id == request.ShopkeeperId, cancellationToken);

        if (shopkeeper is null)
            return Result.Failure("Shopkeeper not found.", "SHOPKEEPER_NOT_FOUND");

        // 3. Verify status
        if (shopkeeper.Status != ShopkeeperApprovalStatus.Pending)
            return Result.Failure(
                $"Shopkeeper is already {shopkeeper.Status}.",
                "INVALID_STATUS");

        // 4. Update shopkeeper status
        shopkeeper.Status = ShopkeeperApprovalStatus.Approved;
        shopkeeper.ApprovedByAccountId = _currentUserService.AccountId;
        shopkeeper.ApprovedAt = _dateTime.UtcNow;
        shopkeeper.SetUpdatedAt(_dateTime.UtcNow);

        // 5. Activate Shop
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id, cancellationToken);

        if (shop is not null)
        {
            shop.IsActive = true;
            shop.SetUpdatedAt(_dateTime.UtcNow);
        }

        // 6. Save
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}