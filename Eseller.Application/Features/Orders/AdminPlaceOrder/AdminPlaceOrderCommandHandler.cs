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
/// SuperAdmin / Partner places an order against any seller (or live warehouse) listing.
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

        if (request.Quantity < 1)
            return Result.Failure<Guid>("Quantity must be at least 1.", "INVALID_QUANTITY");

        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == _currentUserService.AccountId.Value, cancellationToken);

        if (account is null)
            return Result.Failure<Guid>("Admin account not found.", "ACCOUNT_NOT_FOUND");

        // Reuse or create a customer User row for this admin account (orders require UserId)
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value, cancellationToken);

        var now = _dateTime.UtcNow;
        if (user is null)
        {
            user = new User
            {
                AccountId = _currentUserService.AccountId.Value,
                Name = string.IsNullOrWhiteSpace(request.CustomerName)
                    ? (string.IsNullOrWhiteSpace(account.Username) ? "Platform Admin" : account.Username)
                    : request.CustomerName.Trim(),
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
        else if (user.Status != UserApprovalStatus.Approved)
        {
            user.Status = UserApprovalStatus.Approved;
            user.ApprovedByAccountId = _currentUserService.AccountId.Value;
            user.ApprovedAt = now;
            user.RejectionReason = null;
            user.SetUpdatedAt(now);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv =>
                    pv.Id == request.ProductVariantId
                    && pv.IsActive
                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure<Guid>("Variant not available.", "VARIANT_NOT_AVAILABLE");

        if (request.Quantity > variant.StockQty)
            return Result.Failure<Guid>(
                $"Only {variant.StockQty} item(s) available in stock.",
                "INSUFFICIENT_STOCK");

        var masterShopId = await MasterCatalog.TryGetMasterShopIdAsync(_context, cancellationToken);

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                    p.Id == variant.ProductId
                    && p.IsActive
                    && p.IsApproved
                    && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // Admin may order seller listings, or warehouse SKUs that are marked live
        if (masterShopId.HasValue
            && product.ShopId == masterShopId.Value
            && !product.IsStorefrontLive)
        {
            return Result.Failure<Guid>(
                "This warehouse product is not live on the storefront. Enable Live on Website first, or order a seller listing.",
                "PRODUCT_NOT_LIVE");
        }

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == product.ShopId && s.IsActive, cancellationToken);

        if (shop is null)
            return Result.Failure<Guid>("Shop not available.", "SHOP_NOT_AVAILABLE");

        var customerName = !string.IsNullOrWhiteSpace(request.CustomerName)
            ? request.CustomerName.Trim()
            : user.Name;

        var orderRequest = new OrderRequestEntity
        {
            UserId = user.Id,
            Status = OrderStatus.Pending,
            CustomerName = customerName,
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
                ? "Placed by Super Admin"
                : request.OrderNotes.Trim()
        };

        _context.OrderRequests.Add(orderRequest);

        var sellerOrder = new SellerOrder
        {
            OrderRequestId = orderRequest.Id,
            ShopId = shop.Id,
            Status = OrderStatus.Pending
        };
        _context.SellerOrders.Add(sellerOrder);
        await _context.SaveChangesAsync(cancellationToken);

        var attributes = await _context.VariantAttributeValues
            .Where(vav => vav.ProductVariantId == variant.Id)
            .ToListAsync(cancellationToken);

        var attributesDict = attributes
            .GroupBy(a => a.AttributeName)
            .ToDictionary(g => g.Key, g => g.First().AttributeValue);

        var attributesJson = JsonSerializer.Serialize(attributesDict);

        _context.OrderRequestItems.Add(new OrderRequestItem
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
        });

        var previousQty = variant.StockQty;
        variant.StockQty -= request.Quantity;
        variant.SetUpdatedAt(now);

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

        await _context.SaveChangesAsync(cancellationToken);

        var orderItemsForMessage = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == orderRequest.Id)
            .ToListAsync(cancellationToken);

        _context.ChatMessages.Add(new ChatMessageEntity
        {
            OrderRequestId = orderRequest.Id,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = ChatSenderRole.User,
            Message = BuildOrderMessage(orderRequest.Id, orderItemsForMessage, customerName),
            SentAt = now,
            IsRead = false
        });
        await _context.SaveChangesAsync(cancellationToken);

        var shopkeeperAccountId = await (
            from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
            join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where s.Id == shop.Id && s.DeletedAt == null
            select (Guid?)sk.AccountId
        ).FirstOrDefaultAsync(cancellationToken);

        var shortRef = orderRequest.Id.ToString()[..8].ToUpperInvariant();
        var notifTitle = $"New Order #{shortRef}";
        var notifBody = $"{customerName} (Super Admin) placed an order ({request.Quantity} items, Rs. {grossTotal:N0}).";

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
                orderRequest.Id, customerName, request.Quantity, grossTotal, cancellationToken);
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

    private static string BuildOrderMessage(Guid orderId, List<OrderRequestItem> items, string customerName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🛒 New Order Request — #{orderId.ToString()[..8]}");
        sb.AppendLine($"Placed by Super Admin · Buyer: {customerName}");
        sb.AppendLine();

        decimal totalAmount = 0;
        int totalItems = 0;

        foreach (var item in items)
        {
            var attributes = string.IsNullOrEmpty(item.VariantAttributesSnapshot)
                ? string.Empty
                : $" ({item.VariantAttributesSnapshot})";

            sb.AppendLine($"• {item.ProductNameSnapshot}{attributes} × {item.Quantity} — Rs {item.TotalPriceSnapshot:N0}");
            totalAmount += item.TotalPriceSnapshot;
            totalItems += item.Quantity;
        }

        sb.AppendLine();
        sb.AppendLine($"📦 Total Items: {totalItems}");
        sb.AppendLine($"💰 Total Amount: Rs {totalAmount:N0}");
        sb.AppendLine();
        sb.AppendLine("Please review and confirm this order.");

        return sb.ToString();
    }
}
