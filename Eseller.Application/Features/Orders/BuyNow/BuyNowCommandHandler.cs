using System.Text;
using System.Text.Json;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderRequestEntity = Eseller.Domain.Entities.Orders.OrderRequest;
using ChatMessageEntity = Eseller.Domain.Entities.Orders.ChatMessage;

namespace Eseller.Application.Features.Orders.BuyNow;

public class BuyNowCommandHandler
    : IRequestHandler<BuyNowCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _notificationService;
    private readonly IAppNotificationService _notifications;

    public BuyNowCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService notificationService,
        IAppNotificationService notifications)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notificationService = notificationService;
        _notifications = notifications;
    }

    public async Task<Result<Guid>> Handle(
        BuyNowCommand request,
        CancellationToken cancellationToken)
    {
        // ═══════════════════════════════════════════════════════════
        // PART 1 — Validate + Create OrderRequest
        // ═══════════════════════════════════════════════════════════

        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<Guid>("Only customers can place orders.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        if (user.Status != UserApprovalStatus.Approved)
            return Result.Failure<Guid>(
                user.Status == UserApprovalStatus.Rejected
                    ? "Your account was rejected. Contact support."
                    : "Your account is pending Super Admin approval. You cannot place orders yet.",
                "ACCOUNT_PENDING_APPROVAL");

        // 3. Load variant
        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.ProductVariantId
                                    && pv.IsActive
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure<Guid>("Variant not available.", "VARIANT_NOT_AVAILABLE");

        if (request.Quantity > variant.StockQty)
            return Result.Failure<Guid>(
                $"Only {variant.StockQty} item(s) available in stock.",
                "INSUFFICIENT_STOCK");

        // 4. Load product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == variant.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // 5. Load shop
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == product.ShopId && s.IsActive, cancellationToken);

        if (shop is null)
            return Result.Failure<Guid>("Shop not available.", "SHOP_NOT_AVAILABLE");

        // 6. Resolve affiliate
        Guid? affiliateId = null;
        string? referralCodeSnapshot = null;

        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            var normalizedCode = request.ReferralCode.Trim().ToUpperInvariant();
            var affiliate = await _context.Affiliates
                .FirstOrDefaultAsync(a => a.ReferralCode == normalizedCode, cancellationToken);

            if (affiliate is not null)
            {
                if (affiliate.AccountId == _currentUserService.AccountId.Value)
                    return Result.Failure<Guid>("You cannot refer yourself.", "SELF_REFERRAL");

                affiliateId = affiliate.Id;
                referralCodeSnapshot = affiliate.ReferralCode;
            }
        }

        var now = _dateTime.UtcNow;

        // 7. Create OrderRequest
        var orderRequest = new OrderRequestEntity
        {
            UserId = user.Id,
            Status = OrderStatus.Pending,
            AffiliateId = affiliateId,
            ReferralCodeSnapshot = referralCodeSnapshot
        };

        _context.OrderRequests.Add(orderRequest);

        // ═══════════════════════════════════════════════════════════
        // PART 2 — Create SellerOrder + OrderRequestItem + Stock decrement
        // ═══════════════════════════════════════════════════════════

        // 8. Create SellerOrder (single shop)
        var sellerOrder = new SellerOrder
        {
            OrderRequestId = orderRequest.Id,
            ShopId = shop.Id,
            Status = OrderStatus.Pending
        };

        _context.SellerOrders.Add(sellerOrder);

        // Save to get SellerOrder ID
        await _context.SaveChangesAsync(cancellationToken);

        // 9. Load variant attributes for snapshot
        var attributes = await _context.VariantAttributeValues
            .Where(vav => vav.ProductVariantId == variant.Id)
            .ToListAsync(cancellationToken);

        var attributesDict = attributes
            .GroupBy(a => a.AttributeName)
            .ToDictionary(g => g.Key, g => g.First().AttributeValue);

        var attributesJson = JsonSerializer.Serialize(attributesDict);

        // 10. Create OrderRequestItem
        var orderItem = new OrderRequestItem
        {
            OrderRequestId = orderRequest.Id,
            SellerOrderId = sellerOrder.Id,
            ProductVariantId = variant.Id,
            Quantity = request.Quantity,
            PriceAtOrder = variant.Price,
            ProductNameSnapshot = product.Name,
            VariantAttributesSnapshot = attributesJson,
            UnitPriceSnapshot = variant.Price,
            TotalPriceSnapshot = variant.Price * request.Quantity,
            CreatedAt = now
        };

        _context.OrderRequestItems.Add(orderItem);

        // 11. Decrement stock
        var previousQty = variant.StockQty;
        variant.StockQty -= request.Quantity;
        variant.SetUpdatedAt(now);

        // 12. Log InventoryTransaction
        _context.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductVariantId = variant.Id,
            TransactionType = InventoryTransactionType.Order,
            Quantity = -request.Quantity,
            PreviousQuantity = previousQty,
            NewQuantity = variant.StockQty,
            ReferenceType = "OrderRequest",
            ReferenceId = orderRequest.Id,
            CreatedByAccountId = _currentUserService.AccountId.Value,
            CreatedAt = now
        });

        // 12.5. Calculate 20% Net Commission Seller Earning
        var grossTotal = variant.Price * request.Quantity;
        var commissionAmount = Math.Round(grossTotal * 0.20m, 2);

        _context.SellerEarnings.Add(new SellerEarning
        {
            SellerOrderId = sellerOrder.Id,
            GrossAmount = grossTotal,
            CommissionAmount = commissionAmount,
            NetAmount = commissionAmount,
            Status = SellerEarningStatus.Pending
        });

        var keeperAccountId = await _context.Shopkeepers
            .Where(sk => sk.Id == shop.ShopkeeperId)
            .Select(sk => sk.AccountId)
            .FirstOrDefaultAsync(cancellationToken);
        if (keeperAccountId != Guid.Empty && commissionAmount > 0)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.AccountId == keeperAccountId, cancellationToken);
            if (wallet is null)
            {
                wallet = new Eseller.Domain.Entities.Wallet.Wallet
                {
                    AccountId = keeperAccountId,
                    Balance = 0,
                    PendingEarnings = commissionAmount
                };
                wallet.SetCreatedAt(now);
                wallet.SetUpdatedAt(now);
                _context.Wallets.Add(wallet);
            }
            else
            {
                wallet.PendingEarnings += commissionAmount;
                wallet.SetUpdatedAt(now);
            }
        }

        var payment = new Eseller.Domain.Entities.Payment.Payment
        {
            OrderRequestId = orderRequest.Id,
            Method = PaymentMethod.CashOnDelivery,
            Currency = "USD",
            Amount = grossTotal,
            Status = PaymentStatus.Pending
        };
        payment.SetCreatedAt(now);
        payment.SetUpdatedAt(now);
        _context.Payments.Add(payment);

        // 13. Save all
        await _context.SaveChangesAsync(cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // PART 3 — Auto-create first chat message
        // ═══════════════════════════════════════════════════════════

        // 14. Load items for message
        var orderItemsForMessage = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == orderRequest.Id)
            .ToListAsync(cancellationToken);

        var messageText = BuildOrderMessage(orderRequest.Id, orderItemsForMessage);

        // 15. Create auto chat message
        _context.ChatMessages.Add(new ChatMessageEntity
        {
            OrderRequestId = orderRequest.Id,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = ChatSenderRole.User,
            Message = messageText,
            SentAt = _dateTime.UtcNow,
            IsRead = false
        });

        // 16. Notify the shopkeeper + admins
        var shopkeeperAccountId = await (
            from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
            join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where s.Id == shop.Id && s.DeletedAt == null
            select (Guid?)sk.AccountId
        ).FirstOrDefaultAsync(cancellationToken);

        var shortRef = orderRequest.Id.ToString()[..8].ToUpperInvariant();
        var customerLabel = orderRequest.CustomerName ?? user.Name ?? "Customer";
        var notifTitle = $"New Order #{shortRef}";
        var notifBody = $"{customerLabel} placed an order ({request.Quantity} items, ${grossTotal:N0}).";

        if (shopkeeperAccountId.HasValue)
        {
            await _notifications.NotifyAsync(
                shopkeeperAccountId.Value,
                RoleType.Shopkeeper,
                NotificationType.NewOrder,
                notifTitle,
                notifBody,
                NotificationEntityType.Order,
                orderRequest.Id,
                "/orders",
                $"NewOrder:{orderRequest.Id:N}:Seller:{shopkeeperAccountId.Value:N}",
                cancellationToken);
        }

        await _notifications.NotifyAdminsAsync(
            NotificationType.NewOrder,
            notifTitle,
            notifBody,
            NotificationEntityType.Order,
            orderRequest.Id,
            "/orders",
            $"NewOrder:{orderRequest.Id:N}",
            cancellationToken);

        try
        {
            await _notificationService.BroadcastNewOrderAsync(
                orderRequest.Id, customerLabel, request.Quantity, grossTotal, cancellationToken);
        }
        catch { /* best-effort */ }

        if (previousQty > 0 && variant.StockQty <= 0)
        {
            try
            {
                await _notifications.NotifyProductOutOfStockOnceAsync(product.Id, product.Name, cancellationToken);
            }
            catch { /* best-effort */ }
        }

        return Result.Success(orderRequest.Id);
    }

    // ═══════════════════════════════════════════════════════════
    // HELPER — Build order message for chat
    // ═══════════════════════════════════════════════════════════

    private static string BuildOrderMessage(Guid orderId, List<OrderRequestItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🛒 New Order Request — #{orderId.ToString().Substring(0, 8)}");
        sb.AppendLine();

        decimal totalAmount = 0;
        int totalItems = 0;

        foreach (var item in items)
        {
            var attributes = string.IsNullOrEmpty(item.VariantAttributesSnapshot)
                ? string.Empty
                : $" ({item.VariantAttributesSnapshot})";

            sb.AppendLine($"• {item.ProductNameSnapshot}{attributes} × {item.Quantity} — ${item.TotalPriceSnapshot:N0}");

            totalAmount += item.TotalPriceSnapshot;
            totalItems += item.Quantity;
        }

        sb.AppendLine();
        sb.AppendLine($"📦 Total Items: {totalItems}");
        sb.AppendLine($"💰 Total Amount: ${totalAmount:N0}");
        sb.AppendLine();
        sb.AppendLine("Please review and confirm this order.");

        return sb.ToString();
    }
}