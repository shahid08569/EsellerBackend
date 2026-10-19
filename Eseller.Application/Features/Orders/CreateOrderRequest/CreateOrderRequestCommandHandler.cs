using System.Text;
using System.Text.Json;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderRequestEntity = Eseller.Domain.Entities.Orders.OrderRequest;
using ChatMessageEntity = Eseller.Domain.Entities.Orders.ChatMessage;

namespace Eseller.Application.Features.Orders.CreateOrderRequest;

public class CreateOrderRequestCommandHandler 
    : IRequestHandler<CreateOrderRequestCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _notificationService;
    private readonly IAppNotificationService _notifications;

    public CreateOrderRequestCommandHandler(
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
        CreateOrderRequestCommand request,
        CancellationToken cancellationToken)
    {
        // ═══════════════════════════════════════════════════════════
        // PART 1 — Validate + Create OrderRequest
        // ═══════════════════════════════════════════════════════════

        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<Guid>("Only customers can place orders.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        if (user.Status != UserApprovalStatus.Approved)
            return Result.Failure<Guid>(
                user.Status == UserApprovalStatus.Rejected
                    ? "Your account was rejected. Contact support."
                    : "Your account is pending Management approval. You cannot place orders yet.",
                "ACCOUNT_PENDING_APPROVAL");

        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
        {
            cart = new Eseller.Domain.Entities.Orders.Cart { UserId = user.Id };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var cartItems = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        // Resolve line items: either from request.Items (if supplied, e.g. user selected specific cart items) or from database cartItems
        var orderLines = new List<(ProductVariant Variant, int Quantity)>();

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var inputItem in request.Items)
            {
                var qty = inputItem.Quantity > 0 ? inputItem.Quantity : 1;
                // 1. Try finding variant directly
                var variant = await _context.ProductVariants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(pv => pv.Id == inputItem.ProductVariantId
                                            && pv.IsActive
                                            && pv.DeletedAt == null, cancellationToken);

                if (variant is null)
                {
                    // 2. Check if passed ID is actually a ProductId
                    variant = await _context.ProductVariants
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(pv => pv.ProductId == inputItem.ProductVariantId
                                                && pv.IsActive
                                                && pv.DeletedAt == null, cancellationToken);

                    if (variant is null)
                    {
                        var baseProduct = await _context.Products
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(p => p.Id == inputItem.ProductVariantId && p.DeletedAt == null, cancellationToken);

                        if (baseProduct is not null)
                        {
                            variant = new ProductVariant
                            {
                                ProductId = baseProduct.Id,
                                Sku = $"SKU-{(baseProduct.Slug.Length > 8 ? baseProduct.Slug[..8] : baseProduct.Slug).ToUpperInvariant()}-{_dateTime.UtcNow.Ticks % 10000}",
                                Price = baseProduct.BasePrice,
                                StockQty = 100,
                                IsActive = true
                            };
                            variant.SetCreatedAt(_dateTime.UtcNow);
                            variant.SetUpdatedAt(_dateTime.UtcNow);
                            _context.ProductVariants.Add(variant);
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                    }
                }

                if (variant is not null)
                {
                    orderLines.Add((variant, qty));
                }
            }
        }
        else if (cartItems.Count > 0)
        {
            var variantIds = cartItems.Select(ci => ci.ProductVariantId).ToList();
            var variantsMap = await _context.ProductVariants
                .IgnoreQueryFilters()
                .Where(pv => variantIds.Contains(pv.Id))
                .ToDictionaryAsync(pv => pv.Id, cancellationToken);

            foreach (var ci in cartItems)
            {
                var v = variantsMap.GetValueOrDefault(ci.ProductVariantId);
                if (v is not null && v.IsActive && v.DeletedAt == null)
                {
                    orderLines.Add((v, ci.Quantity));
                }
            }
        }

        if (orderLines.Count == 0)
            return Result.Failure<Guid>("Your cart is empty or the selected products are no longer available.", "CART_EMPTY");

        // Validate stock
        foreach (var (variant, quantity) in orderLines)
        {
            if (quantity > variant.StockQty)
                return Result.Failure<Guid>(
                    $"Insufficient stock for SKU {variant.Sku}. Only {variant.StockQty} available.",
                    "INSUFFICIENT_STOCK");
        }

        // Load products
        var productIds = orderLines.Select(l => l.Variant.ProductId).Distinct().ToList();
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in orderLines)
        {
            var p = products.GetValueOrDefault(line.Variant.ProductId);
            if (p is null || !p.IsActive || p.DeletedAt != null)
                return Result.Failure<Guid>("A product in your cart is no longer available.", "PRODUCT_NOT_AVAILABLE");
        }

        // Load shops with fallback to Master Warehouse
        var masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);

        var shopIds = products.Values.Select(p => p.ShopId).Where(id => id != Guid.Empty).Distinct().ToList();
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        shops[masterShop.Id] = masterShop;

        foreach (var prod in products.Values)
        {
            if (prod.ShopId == Guid.Empty || !shops.ContainsKey(prod.ShopId))
            {
                prod.ShopId = masterShop.Id;
            }
        }

        // 9. Resolve affiliate
        Guid? affiliateId = null;
        string? referralCodeSnapshot = null;
        Eseller.Domain.Entities.Identity.Affiliate? affiliateEntity = null;

        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            var normalizedCode = request.ReferralCode.Trim().ToUpperInvariant();
            affiliateEntity = await _context.Affiliates
                .FirstOrDefaultAsync(a => a.ReferralCode == normalizedCode, cancellationToken);

            if (affiliateEntity is not null)
            {
                if (affiliateEntity.AccountId == _currentUserService.AccountId.Value)
                    return Result.Failure<Guid>("You cannot refer yourself.", "SELF_REFERRAL");

                affiliateId = affiliateEntity.Id;
                referralCodeSnapshot = affiliateEntity.ReferralCode;
            }
        }

        // 10. Create OrderRequest
        var orderRequest = new OrderRequestEntity
        {
            UserId = user.Id,
            Status = OrderStatus.Pending,
            AffiliateId = affiliateId,
            ReferralCodeSnapshot = referralCodeSnapshot,
            CustomerName = !string.IsNullOrWhiteSpace(request.CustomerName) ? request.CustomerName.Trim() : user.Name,
            CustomerPhone = !string.IsNullOrWhiteSpace(request.CustomerPhone) ? request.CustomerPhone.Trim() : user.Phone,
            ShippingAddress = request.ShippingAddress?.Trim(),
            City = request.City?.Trim(),
            State = request.State?.Trim(),
            Country = request.Country?.Trim(),
            OrderNotes = request.OrderNotes?.Trim()
        };

        _context.OrderRequests.Add(orderRequest);

        // ═══════════════════════════════════════════════════════════
        // PART 2 — Auto-split into SellerOrder + create OrderRequestItems
        // ═══════════════════════════════════════════════════════════

        var now = _dateTime.UtcNow;

        var itemsByShop = orderLines
            .GroupBy(l => products[l.Variant.ProductId].ShopId)
            .ToList();

        var sellerOrders = new List<SellerOrder>();

        foreach (var shopGroup in itemsByShop)
        {
            var sellerOrder = new SellerOrder
            {
                OrderRequestId = orderRequest.Id,
                ShopId = shopGroup.Key,
                Status = OrderStatus.Pending
            };

            _context.SellerOrders.Add(sellerOrder);
            sellerOrders.Add(sellerOrder);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // ✅ Collect created items for commission (Part 4)
        // ═══════════════════════════════════════════════════════════
        var createdOrderItems = new List<(OrderRequestItem Item, decimal UnitPrice, int Quantity, Guid ProductId)>();
        var outOfStockProducts = new Dictionary<Guid, string>();

        foreach (var shopGroup in itemsByShop)
        {
            var sellerOrder = sellerOrders.First(so => so.ShopId == shopGroup.Key);

            foreach (var (variant, quantity) in shopGroup)
            {
                var product = products[variant.ProductId];

                var attributes = await _context.VariantAttributeValues
                    .Where(vav => vav.ProductVariantId == variant.Id)
                    .ToListAsync(cancellationToken);

                var attributesDict = attributes
                    .GroupBy(a => a.AttributeName)
                    .ToDictionary(g => g.Key, g => g.First().AttributeValue);

                var attributesJson = JsonSerializer.Serialize(attributesDict);

                var orderItem = new OrderRequestItem
                {
                    OrderRequestId = orderRequest.Id,
                    SellerOrderId = sellerOrder.Id,
                    ProductVariantId = variant.Id,
                    Quantity = quantity,
                    PriceAtOrder = variant.Price,
                    ProductNameSnapshot = product.Name,
                    VariantAttributesSnapshot = attributesJson,
                    UnitPriceSnapshot = variant.Price,
                    TotalPriceSnapshot = variant.Price * quantity,
                    CreatedAt = now
                };

                _context.OrderRequestItems.Add(orderItem);

                createdOrderItems.Add((orderItem, variant.Price, quantity, product.Id));

                // Decrement stock
                var previousQty = variant.StockQty;
                variant.StockQty = Math.Max(0, variant.StockQty - quantity);
                variant.SetUpdatedAt(now);

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductVariantId = variant.Id,
                    TransactionType = InventoryTransactionType.Order,
                    Quantity = -quantity,
                    PreviousQuantity = previousQty,
                    NewQuantity = variant.StockQty,
                    ReferenceType = "OrderRequest",
                    ReferenceId = orderRequest.Id,
                    CreatedByAccountId = _currentUserService.AccountId.Value,
                    CreatedAt = now
                });

                if (previousQty > 0 && variant.StockQty <= 0)
                    outOfStockProducts[product.Id] = product.Name;
            }

            // Full gross amount goes to seller earning & wallet pending
            var grossTotal = shopGroup.Sum(line => line.Variant.Price * line.Quantity);

            _context.SellerEarnings.Add(new SellerEarning
            {
                SellerOrderId = sellerOrder.Id,
                GrossAmount = grossTotal,
                CommissionAmount = grossTotal,
                NetAmount = grossTotal,
                Status = SellerEarningStatus.Pending
            });

            // Keep wallet PendingEarnings in sync with ledger (Management Adjust reads wallet)
            var keeperAccountId = await _context.Shops
                .IgnoreQueryFilters()
                .Where(s => s.Id == shopGroup.Key)
                .Join(_context.Shopkeepers, s => s.ShopkeeperId, sk => sk.Id, (_, sk) => sk.AccountId)
                .FirstOrDefaultAsync(cancellationToken);
            if (keeperAccountId != Guid.Empty && grossTotal > 0)
            {
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.AccountId == keeperAccountId, cancellationToken);
                if (wallet is null)
                {
                    wallet = new Eseller.Domain.Entities.Wallet.Wallet
                    {
                        AccountId = keeperAccountId,
                        Balance = 0,
                        PendingEarnings = grossTotal
                    };
                    wallet.SetCreatedAt(now);
                    wallet.SetUpdatedAt(now);
                    _context.Wallets.Add(wallet);
                }
                else
                {
                    wallet.PendingEarnings += grossTotal;
                    wallet.SetUpdatedAt(now);
                }
            }
        }

        // Clear only ordered items from database cart
        var orderedVariantIds = orderLines.Select(l => l.Variant.Id).ToHashSet();
        var itemsToRemove = cartItems.Where(ci => orderedVariantIds.Contains(ci.ProductVariantId)).ToList();
        if (itemsToRemove.Count > 0)
        {
            _context.CartItems.RemoveRange(itemsToRemove);
        }

        // COD payment row — Super Admin marks Completed (Paid) later
        var orderGross = createdOrderItems.Sum(x => x.UnitPrice * x.Quantity);

        var payment = new Eseller.Domain.Entities.Payment.Payment
        {
            OrderRequestId = orderRequest.Id,
            Method = PaymentMethod.CashOnDelivery,
            Currency = "USD",
            Amount = orderGross,
            Status = PaymentStatus.Pending
        };
        payment.SetCreatedAt(now);
        payment.SetUpdatedAt(now);
        _context.Payments.Add(payment);

        await _context.SaveChangesAsync(cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // ✅ PART 4 — AFFILIATE CONVERSION + COMMISSION
        // ═══════════════════════════════════════════════════════════
        if (affiliateId.HasValue && affiliateEntity is not null)
        {
            decimal commissionRate = affiliateEntity.CommissionRate ?? 0m;
            if (commissionRate <= 0)
            {
                var setting = await _context.Settings
                    .FirstOrDefaultAsync(s => s.Key == "AffiliateDefaultCommissionRate", cancellationToken);

                commissionRate = (setting is not null && decimal.TryParse(setting.Value, out var parsed))
                    ? parsed
                    : 5m;
            }
            foreach (var (orderItem, unitPrice, quantity, productId) in createdOrderItems)
            {
                // Find a recent non-converted click for this product (last 30 days)
                var click = await _context.AffiliateClicks
                    .Where(c => c.AffiliateId == affiliateId.Value
                             && c.ProductId == productId
                             && !c.Converted
                             && c.ClickedAt > now.AddDays(-30))
                    .OrderByDescending(c => c.ClickedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                // Calculate commission
                var commissionAmount = Math.Round(
                    unitPrice * quantity * commissionRate / 100m, 2);

                var commission = new Eseller.Domain.Entities.Affiliate.AffiliateCommission
                {
                    AffiliateId = affiliateId.Value,
                    OrderRequestId = orderRequest.Id,
                    OrderRequestItemId = orderItem.Id,
                    AffiliateClickId = click?.Id,
                    CommissionAmount = commissionAmount,
                    Status = AffiliateCommissionStatus.Pending,
                    CreatedAt = now
                };

                _context.AffiliateCommissions.Add(commission);

                // Mark click as converted
                if (click is not null)
                    click.Converted = true;
            }

            // Increment affiliate total conversions + total clicks already handled in track
            affiliateEntity.TotalConversions += 1;
            affiliateEntity.SetUpdatedAt(now);

            await _context.SaveChangesAsync(cancellationToken);
        }

        // Notify each shopkeeper who received a SellerOrder split
        var shopIdsForNotify = sellerOrders.Select(so => so.ShopId).Distinct().ToList();
        var shopkeeperAccountIds = await (
            from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
            join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where shopIdsForNotify.Contains(s.Id) && s.DeletedAt == null
            select sk.AccountId
        ).Distinct().ToListAsync(cancellationToken);

        var orderTotal = createdOrderItems.Sum(i => i.UnitPrice * i.Quantity);
        var itemCount = createdOrderItems.Sum(i => i.Quantity);
        var shortRef = orderRequest.Id.ToString()[..8].ToUpperInvariant();
        var notifTitle = $"New Order #{shortRef}";
        var notifBody = $"{orderRequest.CustomerName ?? "Customer"} placed an order ({itemCount} items, ${orderTotal:N0}).";

        foreach (var accountId in shopkeeperAccountIds)
        {
            await _notifications.NotifyAsync(
                accountId,
                RoleType.Shopkeeper,
                NotificationType.NewOrder,
                notifTitle,
                notifBody,
                NotificationEntityType.Order,
                orderRequest.Id,
                "/orders",
                $"NewOrder:{orderRequest.Id:N}:Seller:{accountId:N}",
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
                orderRequest.Id,
                orderRequest.CustomerName ?? "Customer",
                itemCount,
                orderTotal,
                cancellationToken);
        }
        catch { /* best-effort */ }

        foreach (var (productId, productName) in outOfStockProducts)
        {
            try
            {
                await _notifications.NotifyProductOutOfStockOnceAsync(productId, productName, cancellationToken);
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