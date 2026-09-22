using System.Text;
using System.Text.Json;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Orders;
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

    public CreateOrderRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
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

        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
            return Result.Failure<Guid>("Cart is empty.", "CART_EMPTY");

        var cartItems = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
            return Result.Failure<Guid>("Cart is empty.", "CART_EMPTY");

        var variantIds = cartItems.Select(ci => ci.ProductVariantId).ToList();

        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id))
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        foreach (var cartItem in cartItems)
        {
            var variant = variants.GetValueOrDefault(cartItem.ProductVariantId);

            if (variant is null || !variant.IsActive || variant.DeletedAt != null)
                return Result.Failure<Guid>(
                    "A product in your cart is no longer available.",
                    "VARIANT_NOT_AVAILABLE");

            if (cartItem.Quantity > variant.StockQty)
                return Result.Failure<Guid>(
                    $"Insufficient stock for SKU {variant.Sku}. Only {variant.StockQty} available.",
                    "INSUFFICIENT_STOCK");
        }

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var product in products.Values)
        {
            if (!product.IsActive || !product.IsApproved || product.DeletedAt != null)
                return Result.Failure<Guid>(
                    $"Product '{product.Name}' is no longer available.",
                    "PRODUCT_NOT_AVAILABLE");
        }

        var shopIds = products.Values.Select(p => p.ShopId).Distinct().ToList();

        var shops = await _context.Shops
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var shop in shops.Values)
        {
            if (!shop.IsActive)
                return Result.Failure<Guid>(
                    $"Shop '{shop.Name}' is no longer available.",
                    "SHOP_NOT_AVAILABLE");
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
            ReferralCodeSnapshot = referralCodeSnapshot
        };

        _context.OrderRequests.Add(orderRequest);

        // ═══════════════════════════════════════════════════════════
        // PART 2 — Auto-split into SellerOrder + create OrderRequestItems
        // ═══════════════════════════════════════════════════════════

        var now = _dateTime.UtcNow;

        var itemsByShop = cartItems
            .GroupBy(ci =>
            {
                var variant = variants[ci.ProductVariantId];
                var product = products[variant.ProductId];
                return product.ShopId;
            })
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
        // ✅ AFFILIATE — Collect created items for commission (Part 4)
        // ═══════════════════════════════════════════════════════════
        var createdOrderItems = new List<(OrderRequestItem Item, decimal UnitPrice, int Quantity, Guid ProductId)>();

        foreach (var shopGroup in itemsByShop)
        {
            var sellerOrder = sellerOrders.First(so => so.ShopId == shopGroup.Key);

            foreach (var cartItem in shopGroup)
            {
                var variant = variants[cartItem.ProductVariantId];
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
                    Quantity = cartItem.Quantity,
                    PriceAtOrder = variant.Price,
                    ProductNameSnapshot = product.Name,
                    VariantAttributesSnapshot = attributesJson,
                    UnitPriceSnapshot = variant.Price,
                    TotalPriceSnapshot = variant.Price * cartItem.Quantity,
                    CreatedAt = now
                };

                _context.OrderRequestItems.Add(orderItem);

                // ✅ Collect for affiliate commission
                createdOrderItems.Add((orderItem, variant.Price, cartItem.Quantity, product.Id));

                // Decrement stock
                var previousQty = variant.StockQty;
                variant.StockQty -= cartItem.Quantity;
                variant.SetUpdatedAt(now);

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductVariantId = variant.Id,
                    TransactionType = InventoryTransactionType.Order,
                    Quantity = -cartItem.Quantity,
                    PreviousQuantity = previousQty,
                    NewQuantity = variant.StockQty,
                    ReferenceType = "OrderRequest",
                    ReferenceId = orderRequest.Id,
                    CreatedByAccountId = _currentUserService.AccountId.Value,
                    CreatedAt = now
                });
            }
        }

        // Clear cart
        _context.CartItems.RemoveRange(cartItems);

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

        // ═══════════════════════════════════════════════════════════
        // PART 3 — Auto-create first chat message
        // ═══════════════════════════════════════════════════════════

        var orderItemsForMessage = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == orderRequest.Id)
            .ToListAsync(cancellationToken);

        var messageText = BuildOrderMessage(orderRequest.Id, orderItemsForMessage);

        _context.ChatMessages.Add(new ChatMessageEntity
        {
            OrderRequestId = orderRequest.Id,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = ChatSenderRole.User,
            Message = messageText,
            SentAt = _dateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync(cancellationToken);

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