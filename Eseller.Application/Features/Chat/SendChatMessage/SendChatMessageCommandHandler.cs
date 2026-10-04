using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ChatMessageEntity = Eseller.Domain.Entities.Orders.ChatMessage;

namespace Eseller.Application.Features.Chat.SendChatMessage;

public class SendChatMessageCommandHandler
    : IRequestHandler<SendChatMessageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _notificationService;

    public SendChatMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService notificationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notificationService = notificationService;
    }

    public async Task<Result<Guid>> Handle(
        SendChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderRequestId, cancellationToken);

        if (order is null)
        {
            // Shop support channel (admin ↔ seller) uses Shop.Id as the chat room key
            order = await ShopSupportChannel.EnsureAsync(
                _context,
                request.OrderRequestId,
                _dateTime.UtcNow,
                cancellationToken);

            if (order is null)
                return Result.Failure<Guid>("Order or shop support channel not found.", "ORDER_NOT_FOUND");
        }
        else if (order.UserId == Guid.Empty)
        {
            var fixedOrder = await ShopSupportChannel.EnsureAsync(
                _context,
                request.OrderRequestId,
                _dateTime.UtcNow,
                cancellationToken);
            if (fixedOrder is null)
                return Result.Failure<Guid>("Support channel is missing a valid user.", "ORDER_INVALID");
            order = fixedOrder;
        }

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isCustomer = role == RoleType.User;
        var isShopkeeper = role == RoleType.Shopkeeper;

        ChatSenderRole senderRole;

        if (isAdmin)
        {
            senderRole = role == RoleType.SuperAdmin
                ? ChatSenderRole.SuperAdmin
                : ChatSenderRole.Partner;
        }
        else if (isShopkeeper)
        {
            // Sellers may only message Super Admin on shop-support channels.
            var allowed = await ShopkeeperCanAccessSupportAsync(order, cancellationToken);
            if (!allowed)
                return Result.Failure<Guid>(
                    "You can only chat with Super Admin (shop support).",
                    "FORBIDDEN");

            senderRole = ChatSenderRole.Shopkeeper;
        }
        else if (isCustomer)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null)
            {
                var acc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == _currentUserService.AccountId.Value, cancellationToken);
                user = new User
                {
                    AccountId = _currentUserService.AccountId.Value,
                    Name = acc?.Username ?? "Customer"
                };
                user.SetCreatedAt(_dateTime.UtcNow);
                _context.Users.Add(user);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Customers may only message Super Admin on their support channel.
            var supportMarker = $"CUSTOMER_SUPPORT_{user.Id}";
            if (!string.Equals(order.ShippingAddress, supportMarker, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<Guid>(
                    "You can only chat with Super Admin (platform support).",
                    "FORBIDDEN");

            if (order.UserId != user.Id)
            {
                if (order.UserId == Guid.Empty)
                {
                    order.UserId = user.Id;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
                }
            }

            senderRole = ChatSenderRole.User;
        }
        else
        {
            return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
        }

        var now = _dateTime.UtcNow;

        var chatMessage = new ChatMessageEntity
        {
            OrderRequestId = order.Id,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = senderRole,
            Message = request.Message.Trim(),
            SentAt = now,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync(cancellationToken);

        var notifyAccountIds = await ResolveNotifyAccountIdsAsync(
            order,
            _currentUserService.AccountId.Value,
            isAdmin,
            isShopkeeper,
            isCustomer,
            cancellationToken);

        // Persist bell notifications (admin + seller + customer)
        var preview = chatMessage.Message.Length > 120
            ? chatMessage.Message[..117] + "..."
            : chatMessage.Message;
        var senderLabel = isAdmin ? "Admin" : isShopkeeper ? "Seller" : "Customer";

        foreach (var accountId in notifyAccountIds)
        {
            var notif = new Notification
            {
                AccountId = accountId,
                Type = NotificationType.NewChatMessage,
                Title = $"New message from {senderLabel}",
                Message = preview,
                IsRead = false
            };
            notif.SetCreatedAt(now);
            notif.SetUpdatedAt(now);
            _context.Notifications.Add(notif);
        }

        if (notifyAccountIds.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.BroadcastChatMessageAsync(
            order.Id,
            chatMessage.Id,
            _currentUserService.AccountId.Value,
            senderRole.ToString(),
            chatMessage.Message,
            now,
            cancellationToken,
            notifyAccountIds);

        return Result.Success(chatMessage.Id);
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveNotifyAccountIdsAsync(
        OrderRequest order,
        Guid senderAccountId,
        bool senderIsAdmin,
        bool senderIsShopkeeper,
        bool senderIsCustomer,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid>();

        Guid? shopkeeperAccountId = null;
        Guid? customerAccountId = null;

        // Shop ↔ admin support: OrderRequest.Id == Shop.Id
        var shop = await _context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == order.Id && s.DeletedAt == null, cancellationToken);

        if (shop is null
            && !string.IsNullOrWhiteSpace(order.ShippingAddress)
            && order.ShippingAddress.StartsWith("SHOP_SUPPORT_", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(order.ShippingAddress["SHOP_SUPPORT_".Length..], out var supportShopId))
        {
            shop = await _context.Shops
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == supportShopId && s.DeletedAt == null, cancellationToken);
        }

        if (shop is not null)
        {
            var keeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);
            if (keeper is not null)
                shopkeeperAccountId = keeper.AccountId;
        }

        // Customer ↔ shop channel marker SHOP_CHAT_{shopId}_{userId}
        if (!string.IsNullOrWhiteSpace(order.ShippingAddress)
            && order.ShippingAddress.StartsWith("SHOP_CHAT_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = order.ShippingAddress.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && Guid.TryParse(parts[2], out var chatShopId))
            {
                var chatShop = await _context.Shops
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == chatShopId && s.DeletedAt == null, cancellationToken);
                if (chatShop is not null)
                {
                    var keeper = await _context.Shopkeepers
                        .FirstOrDefaultAsync(sk => sk.Id == chatShop.ShopkeeperId, cancellationToken);
                    if (keeper is not null)
                        shopkeeperAccountId = keeper.AccountId;
                }
            }
        }

        // Regular order / support channel customer
        if (order.UserId != Guid.Empty)
        {
            var customer = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == order.UserId, cancellationToken);
            if (customer is not null)
                customerAccountId = customer.AccountId;
        }

        // Regular marketplace order → seller via SellerOrders
        if (!shopkeeperAccountId.HasValue)
        {
            var sellerShopId = await _context.SellerOrders
                .Where(so => so.OrderRequestId == order.Id)
                .Select(so => (Guid?)so.ShopId)
                .FirstOrDefaultAsync(cancellationToken);
            if (sellerShopId.HasValue)
            {
                var sellerShop = await _context.Shops
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == sellerShopId.Value && s.DeletedAt == null, cancellationToken);
                if (sellerShop is not null)
                {
                    var keeper = await _context.Shopkeepers
                        .FirstOrDefaultAsync(sk => sk.Id == sellerShop.ShopkeeperId, cancellationToken);
                    if (keeper is not null)
                        shopkeeperAccountId = keeper.AccountId;
                }
            }
        }

        // SuperAdmin mediation only — never cross-notify seller ↔ customer.
        if (senderIsAdmin)
        {
            var addr = order.ShippingAddress ?? string.Empty;
            if (addr.StartsWith("CUSTOMER_SUPPORT_", StringComparison.OrdinalIgnoreCase)
                || addr.Equals("Live Support Inquiry", StringComparison.OrdinalIgnoreCase))
            {
                if (customerAccountId.HasValue)
                    recipients.Add(customerAccountId.Value);
            }
            else if (shopkeeperAccountId.HasValue)
            {
                recipients.Add(shopkeeperAccountId.Value);
            }
        }
        else if (senderIsShopkeeper || senderIsCustomer)
        {
            var adminIds = await _context.Accounts
                .Where(a => a.RoleType == RoleType.SuperAdmin || a.RoleType == RoleType.Partner)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in adminIds)
                recipients.Add(id);
        }

        recipients.Remove(senderAccountId);
        return recipients;
    }

    private async Task<bool> ShopkeeperCanAccessSupportAsync(
        OrderRequest order,
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

        // Shop support channel: OrderRequest.Id == Shop.Id
        if (shops.Contains(order.Id))
            return true;

        if (!string.IsNullOrWhiteSpace(order.ShippingAddress))
        {
            foreach (var shopId in shops)
            {
                if (order.ShippingAddress.Equals($"SHOP_SUPPORT_{shopId}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}