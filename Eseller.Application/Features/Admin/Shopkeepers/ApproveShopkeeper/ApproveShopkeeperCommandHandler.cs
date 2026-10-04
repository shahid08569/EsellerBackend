using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Wallet;
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
    private readonly ISignalRNotificationService _signalR;

    public ApproveShopkeeperCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService signalR)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _signalR = signalR;
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

        var now = _dateTime.UtcNow;
        var notif = new Notification
        {
            AccountId = shopkeeper.AccountId,
            Type = NotificationType.ShopApproved,
            Title = "Store verified",
            Message = shop is not null
                ? $"Your store \"{shop.Name}\" has been approved. You can now list products and sell."
                : "Your seller account has been approved. You can now list products and sell.",
            IsRead = false
        };
        notif.SetCreatedAt(now);
        notif.SetUpdatedAt(now);
        _context.Notifications.Add(notif);

        await _context.SaveChangesAsync(cancellationToken);

        await _signalR.SendNotificationToAccountAsync(
            shopkeeper.AccountId,
            notif.Id,
            notif.Type,
            notif.Title,
            notif.Message,
            cancellationToken);

        return Result.Success();
    }
}
