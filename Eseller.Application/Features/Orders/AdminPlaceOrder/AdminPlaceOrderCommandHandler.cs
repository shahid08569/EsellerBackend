using System.Text;
using System.Text.Json;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderRequestEntity = Eseller.Domain.Entities.Orders.OrderRequest;
using ChatMessageEntity = Eseller.Domain.Entities.Orders.ChatMessage;

namespace Eseller.Application.Features.Orders.AdminPlaceOrder;

/// <summary>
/// SuperAdmin / Partner places an order against any seller (or live warehouse) listings.
/// Supports both single-product orders and multi-product orders at a time.
/// Creates/reuses a customer User profile linked to the admin Account so orders appear in the normal list.
/// </summary>
public class AdminPlaceOrderCommandHandler
    : IRequestHandler<AdminPlaceOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _notificationService;
    private readonly IAppNotificationService _notifications;

    public AdminPlaceOrderCommandHandler(
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
        AdminPlaceOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>("Only Super Admin can place platform orders.", "FORBIDDEN");

        if (role == RoleType.Partner)
        {
            var hasPerm = await PartnerPermissionChecker.HasPermissionAsync(
                _context, _currentUserService, "orders:place", cancellationToken);
            if (!hasPerm)
                return Result.Failure<Guid>("You do not have permission to place orders to merchants.", "FORBIDDEN");
        }

        // 1. Gather all requested items (support both multi-item list and fallback single item)
        var rawItems = new List<AdminPlaceOrderItemDto>();
        if (request.Items != null && request.Items.Count > 0)
        {
            rawItems.AddRange(request.Items.Where(i => i.Quantity > 0));
        }
        else if (request.ProductVariantId.HasValue && request.Quantity.HasValue && request.Quantity.Value > 0)
        {
            rawItems.Add(new AdminPlaceOrderItemDto(request.ProductVariantId.Value, request.Quantity.Value));
        }

        if (rawItems.Count == 0)
            return Result.Failure<Guid>("Please select at least one item with valid quantity.", "INVALID_ITEMS");

        // Consolidate duplicate variant IDs
        var consolidatedItems = rawItems
            .GroupBy(i => i.ProductVariantId)
            .Select(g => new AdminPlaceOrderItemDto(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == _currentUserService.AccountId.Value, cancellationToken);

        if (account is null)
            return Result.Failure<Guid>("Admin account not found.", "ACCOUNT_NOT_FOUND");

        var now = _dateTime.UtcNow;

        // Reuse or create a customer User row for this admin account (orders require UserId)
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value, cancellationToken);

        var desiredCustomerName = !string.IsNullOrWhiteSpace(request.CustomerName)
            ? request.CustomerName.Trim()
            : (!string.IsNullOrWhiteSpace(account.Username) ? account.Username : "Customer");

        if (user is null)
        {
            user = new User
            {
                AccountId = _currentUserService.AccountId.Value,
                Name = desiredCustomerName,
                Phone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : request.CustomerPhone.Trim(),
                Status = UserApprovalStatus.Approved,
                ApprovedByAccountId = _currentUserService.AccountId.Value,
                ApprovedAt = now
            };
            user.SetCreatedAt(now);
            user.SetUpdatedAt(now);
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var updated = false;
            if (!string.IsNullOrWhiteSpace(request.CustomerName) && user.Name != desiredCustomerName)
            {
                user.Name = desiredCustomerName;
                updated = true;
            }
            if (user.Status != UserApprovalStatus.Approved)
            {
                user.Status = UserApprovalStatus.Approved;
                user.ApprovedByAccountId = _currentUserService.AccountId.Value;
                user.ApprovedAt = now;
                user.RejectionReason = null;
                updated = true;
            }
            if (updated)
            {
                user.SetUpdatedAt(now);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // 2. Fetch and validate variants
        var variantIds = consolidatedItems.Select(i => i.ProductVariantId).ToList();
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id) && pv.IsActive && pv.DeletedAt == null)
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        foreach (var item in consolidatedItems)
        {
            if (!variants.TryGetValue(item.ProductVariantId, out var variant))
                return Result.Failure<Guid>("One or more selected product variants are not available.", "VARIANT_NOT_AVAILABLE");

            if (item.Quantity > variant.StockQty)
            {
                // For admin placed orders, automatically replenish stock to fulfill the order
                variant.StockQty = item.Quantity + 50;
                variant.SetUpdatedAt(now);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // 3. Fetch and validate products
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id) && p.IsActive && (p.IsApproved || (masterShopId.HasValue && p.ShopId == masterShopId.Value)) && p.DeletedAt == null)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var variant in variants.Values)
        {
            if (!products.TryGetValue(variant.ProductId, out var product))
                return Result.Failure<Guid>("One or more products are not available.", "PRODUCT_NOT_AVAILABLE");

            if (masterShopId.HasValue && product.ShopId == masterShopId.Value && !product.IsStorefrontLive)
            {
                return Result.Failure<Guid>(
                    $"Warehouse product '{product.Name}' is not live on the storefront. Enable Live on Website first.",
                    "PRODUCT_NOT_LIVE");
            }
        }

        // 4. Fetch and validate shops
        var shopIds = products.Values.Select(p => p.ShopId).Distinct().ToList();
        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id) && s.IsActive)
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var shopId in shopIds)
        {
            if (!shops.ContainsKey(shopId))
                return Result.Failure<Guid>("One or more seller shops are not active.", "SHOP_NOT_AVAILABLE");
        }

        if (role == RoleType.Partner)
        {
            var partner = await _context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == _currentUserService.AccountId.Value, cancellationToken);
            var partnerId = partner?.Id ?? Guid.Empty;

            foreach (var shopId in shopIds)
            {
                if (!shops.TryGetValue(shopId, out var targetShop) || targetShop.PartnerId != partnerId)
                {
                    return Result.Failure<Guid>(
                        "Partners can only place orders to shops that registered through their own referral link.",
                        "FORBIDDEN");
                }
            }
        }

        // 5. Create OrderRequest
        var orderRequest = new OrderRequestEntity
        {
            UserId = user.Id,
            Status = OrderStatus.Pending,
            CustomerName = desiredCustomerName,
            CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone)
                ? user.Phone
                : request.CustomerPhone.Trim(),
            ShippingAddress = string.IsNullOrWhiteSpace(request.ShippingAddress)
                ? null
                : request.ShippingAddress.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            State = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim(),
            OrderNotes = string.IsNullOrWhiteSpace(request.OrderNotes)
                ? "Placed by Management"
                : request.OrderNotes.Trim()
        };

        _context.OrderRequests.Add(orderRequest);

        // 6. Create SellerOrders (grouped by ShopId)
        var itemsByShop = consolidatedItems
            .GroupBy(item => products[variants[item.ProductVariantId].ProductId].ShopId)
            .ToList();

        var sellerOrders = new Dictionary<Guid, SellerOrder>();
        foreach (var shopGroup in itemsByShop)
        {
            var sellerOrder = new SellerOrder
            {
                OrderRequestId = orderRequest.Id,
                ShopId = shopGroup.Key,
                Status = OrderStatus.Pending
            };
            _context.SellerOrders.Add(sellerOrder);
            sellerOrders[shopGroup.Key] = sellerOrder;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 7. Create OrderRequestItems, update inventory, record inventory transactions
        var allAttributes = await _context.VariantAttributeValues
            .Where(vav => variantIds.Contains(vav.ProductVariantId))
            .ToListAsync(cancellationToken);

        var attributesByVariant = allAttributes
            .GroupBy(a => a.ProductVariantId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.AttributeName).ToDictionary(x => x.Key, x => x.First().AttributeValue));

        var createdItems = new List<OrderRequestItem>();
        decimal grandGrossTotal = 0;
        var grossByShop = new Dictionary<Guid, decimal>();
        var outOfStockProducts = new List<Product>();

        foreach (var item in consolidatedItems)
        {
            var variant = variants[item.ProductVariantId];
            var product = products[variant.ProductId];
            var sellerOrder = sellerOrders[product.ShopId];

            string attributesJson = "{}";
            if (attributesByVariant.TryGetValue(variant.Id, out var dict))
            {
                attributesJson = JsonSerializer.Serialize(dict);
            }

            var lineGross = variant.Price * item.Quantity;
            grandGrossTotal += lineGross;
            grossByShop[product.ShopId] = grossByShop.GetValueOrDefault(product.ShopId) + lineGross;

            var orderItem = new OrderRequestItem
            {
                OrderRequestId = orderRequest.Id,
                SellerOrderId = sellerOrder.Id,
                ProductVariantId = variant.Id,
                Quantity = item.Quantity,
                PriceAtOrder = variant.Price,
                ProductNameSnapshot = product.Name,
                VariantAttributesSnapshot = attributesJson,
                UnitPriceSnapshot = variant.Price,
                TotalPriceSnapshot = lineGross,
                CreatedAt = now
            };
            _context.OrderRequestItems.Add(orderItem);
            createdItems.Add(orderItem);

            var previousQty = variant.StockQty;
            variant.StockQty -= item.Quantity;
            variant.SetUpdatedAt(now);

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductVariantId = variant.Id,
                TransactionType = InventoryTransactionType.Order,
                Quantity = -item.Quantity,
                PreviousQuantity = previousQty,
                NewQuantity = variant.StockQty,
                ReferenceType = "OrderRequest",
                ReferenceId = orderRequest.Id,
                CreatedByAccountId = _currentUserService.AccountId.Value,
                CreatedAt = now
            });

            if (previousQty > 0 && variant.StockQty <= 0)
            {
                outOfStockProducts.Add(product);
            }
        }

        // 8. Create SellerEarnings per shop and credit pending wallet (full order value)
        foreach (var (shopId, shopGross) in grossByShop)
        {
            var sellerOrder = sellerOrders[shopId];

            _context.SellerEarnings.Add(new SellerEarning
            {
                SellerOrderId = sellerOrder.Id,
                GrossAmount = shopGross,
                CommissionAmount = shopGross,
                NetAmount = shopGross,
                Status = SellerEarningStatus.Pending
            });

            var shop = shops[shopId];
            var keeperAccountId = await _context.Shopkeepers
                .Where(sk => sk.Id == shop.ShopkeeperId)
                .Select(sk => sk.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (keeperAccountId != Guid.Empty && shopGross > 0)
            {
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.AccountId == keeperAccountId, cancellationToken);
                if (wallet is null)
                {
                    wallet = new Eseller.Domain.Entities.Wallet.Wallet
                    {
                        AccountId = keeperAccountId,
                        Balance = 0,
                        PendingEarnings = shopGross
                    };
                    wallet.SetCreatedAt(now);
                    wallet.SetUpdatedAt(now);
                    _context.Wallets.Add(wallet);
                }
                else
                {
                    wallet.PendingEarnings += shopGross;
                    wallet.SetUpdatedAt(now);
                }
            }
        }

        // 9. Payment record for total amount
        var payment = new Eseller.Domain.Entities.Payment.Payment
        {
            OrderRequestId = orderRequest.Id,
            Method = PaymentMethod.CashOnDelivery,
            Currency = "USD",
            Amount = grandGrossTotal,
            Status = PaymentStatus.Pending
        };
        payment.SetCreatedAt(now);
        payment.SetUpdatedAt(now);
        _context.Payments.Add(payment);

        await _context.SaveChangesAsync(cancellationToken);

        // 10. SuperAdmin orders do not send chat message to merchant — notification only

        // 11. Notifications
        var totalQuantity = consolidatedItems.Sum(i => i.Quantity);
        var shortRef = orderRequest.Id.ToString()[..8].ToUpperInvariant();
        var notifTitle = $"New Order #{shortRef}";
        var notifBody = $"{desiredCustomerName} placed an order ({totalQuantity} items, ${grandGrossTotal:N0}).";

        foreach (var (shopId, shopGross) in grossByShop)
        {
            var shop = shops[shopId];
            var keeperAccountId = await _context.Shopkeepers
                .Where(sk => sk.Id == shop.ShopkeeperId)
                .Select(sk => sk.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (keeperAccountId != Guid.Empty)
            {
                var shopOrderItems = createdItems.Where(i => i.SellerOrderId == sellerOrders[shopId].Id).ToList();
                var shopQuantity = shopOrderItems.Sum(i => i.Quantity);
                var shopNotifBody = $"{desiredCustomerName} placed an order for {shop.Name} ({shopQuantity} items, ${shopGross:N0}).";

                await _notifications.NotifyAsync(
                    keeperAccountId,
                    RoleType.Shopkeeper,
                    NotificationType.NewOrder,
                    notifTitle,
                    shopNotifBody,
                    NotificationEntityType.Order,
                    orderRequest.Id,
                    "/orders",
                    $"NewOrder:{orderRequest.Id:N}:Seller:{keeperAccountId:N}",
                    cancellationToken);
            }
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
                orderRequest.Id, desiredCustomerName, totalQuantity, grandGrossTotal, cancellationToken);
        }
        catch { /* best-effort */ }

        foreach (var p in outOfStockProducts)
        {
            try
            {
                await _notifications.NotifyProductOutOfStockOnceAsync(p.Id, p.Name, cancellationToken);
            }
            catch { /* best-effort */ }
        }

        return Result.Success(orderRequest.Id);
    }

    private static string BuildOrderMessage(Guid orderId, List<OrderRequestItem> items, string customerName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🛒 New Order Request — #{orderId.ToString()[..8]}");
        sb.AppendLine($"Placed by Customer Support · Buyer: {customerName}");
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
