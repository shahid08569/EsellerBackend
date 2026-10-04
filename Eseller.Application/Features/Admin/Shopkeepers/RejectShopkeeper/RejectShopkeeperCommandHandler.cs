using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Admin.Shopkeepers.RejectShopkeeper;

public class RejectShopkeeperCommandHandler
    : IRequestHandler<RejectShopkeeperCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public RejectShopkeeperCommandHandler(
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
        RejectShopkeeperCommand request,
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

        shopkeeper.Status = ShopkeeperApprovalStatus.Rejected;
        shopkeeper.ApprovedByAccountId = _currentUserService.AccountId;
        shopkeeper.ApprovedAt = _dateTime.UtcNow;
        shopkeeper.RejectionReason = request.Reason;
        shopkeeper.SetUpdatedAt(_dateTime.UtcNow);

        var now = _dateTime.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Please contact support for details."
            : request.Reason.Trim();

        var notif = new Notification
        {
            AccountId = shopkeeper.AccountId,
            Type = NotificationType.ShopRejected,
            Title = "Store verification rejected",
            Message = $"Your seller application was not approved. Reason: {reason}",
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
