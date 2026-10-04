using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.MarkMessageAsRead;

public class MarkMessageAsReadCommandHandler
    : IRequestHandler<MarkMessageAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public MarkMessageAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        MarkMessageAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(cm => cm.Id == request.MessageId, cancellationToken);

        if (message is null)
            return Result.Failure("Message not found.", "MESSAGE_NOT_FOUND");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == message.OrderRequestId, cancellationToken);

        if (order is null)
            return Result.Failure("Order not found.", "ORDER_NOT_FOUND");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            if (role == RoleType.Shopkeeper)
            {
                if (!await ShopkeeperCanAccessOrderAsync(order, cancellationToken))
                    return Result.Failure("You do not have permission.", "FORBIDDEN");
            }
            else
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                        cancellationToken);

                if (user is null || order.UserId != user.Id)
                    return Result.Failure("You do not have permission.", "FORBIDDEN");
            }
        }

        if (message.SenderAccountId == _currentUserService.AccountId.Value)
            return Result.Success();

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = _dateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<bool> ShopkeeperCanAccessOrderAsync(
        Domain.Entities.Orders.OrderRequest order,
        CancellationToken cancellationToken)
    {
        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId!.Value, cancellationToken);
        if (shopkeeper is null)
            return false;

        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (shops.Contains(order.Id))
            return true;

        if (!string.IsNullOrWhiteSpace(order.ShippingAddress))
        {
            foreach (var shopId in shops)
            {
                if (order.ShippingAddress.StartsWith($"SHOP_CHAT_{shopId}_", StringComparison.OrdinalIgnoreCase)
                    || order.ShippingAddress.Equals($"SHOP_SUPPORT_{shopId}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return await _context.SellerOrders
            .AnyAsync(so => so.OrderRequestId == order.Id && shops.Contains(so.ShopId), cancellationToken);
    }
}
