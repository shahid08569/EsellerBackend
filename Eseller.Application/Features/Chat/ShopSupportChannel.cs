using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat;

/// <summary>
/// Ensures a shop↔admin support chat channel exists.
/// Channels reuse OrderRequest with Id = Shop.Id and a valid UserId (FK).
/// </summary>
public static class ShopSupportChannel
{
    public static async Task<OrderRequest?> EnsureAsync(
        IApplicationDbContext context,
        Guid channelId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var order = await context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == channelId, cancellationToken);

        if (order is not null)
        {
            var dirty = false;
            if (order.UserId == Guid.Empty)
            {
                var userId = await ResolveSupportUserIdAsync(context, channelId, utcNow, cancellationToken);
                if (userId is null)
                    return null;

                order.UserId = userId.Value;
                dirty = true;
            }

            // Legacy support channels were created as Pending — clear false "Pending" badge
            if (!string.IsNullOrWhiteSpace(order.ShippingAddress)
                && order.ShippingAddress.StartsWith("SHOP_SUPPORT_", StringComparison.OrdinalIgnoreCase)
                && order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.Confirmed;
                dirty = true;
            }

            if (dirty)
                await context.SaveChangesAsync(cancellationToken);

            return order;
        }

        var shop = await context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == channelId && s.DeletedAt == null, cancellationToken);

        if (shop is null)
            return null;

        var supportUserId = await ResolveSupportUserIdAsync(context, shop.Id, utcNow, cancellationToken);
        if (supportUserId is null)
            return null;

        order = new OrderRequest
        {
            Id = shop.Id,
            UserId = supportUserId.Value,
            CustomerName = shop.Name,
            ShippingAddress = $"SHOP_SUPPORT_{shop.Id}",
            City = shop.City ?? "Online",
            Country = shop.Country,
            OrderNotes = "Shop ↔ Super Admin support channel",
            // Not a commerce order — Confirmed avoids false "Pending" badges in admin chat UI
            Status = OrderStatus.Confirmed
        };
        order.SetCreatedAt(utcNow);
        order.SetUpdatedAt(utcNow);

        context.OrderRequests.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        return order;
    }

    private static async Task<Guid?> ResolveSupportUserIdAsync(
        IApplicationDbContext context,
        Guid shopId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var shop = await context.Shops
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == shopId, cancellationToken);

        if (shop is null)
            return null;

        var shopkeeper = await context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null)
            return null;

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.AccountId == shopkeeper.AccountId, cancellationToken);

        if (user is not null)
            return user.Id;

        // Shopkeeper accounts normally have no User row; create one for the OrderRequest FK.
        user = new User
        {
            AccountId = shopkeeper.AccountId,
            Name = shopkeeper.Name,
            Phone = shopkeeper.Phone
        };
        user.SetCreatedAt(utcNow);
        user.SetUpdatedAt(utcNow);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
