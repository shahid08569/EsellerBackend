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

        // 3. Get cart
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
            return Result.Failure<Guid>("Cart is empty.", "CART_EMPTY");

        // 4. Get cart items
        var cartItems = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
            return Result.Failure<Guid>("Cart is empty.", "CART_EMPTY");

        // 5. Load variants
        var variantIds = cartItems.Select(ci => ci.ProductVariantId).ToList();

        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id))
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        // 6. Validate variants
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

        // 7. Load + validate products
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

        // 8. Load + validate shops
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

        // 11. Group cart items by shop
        var itemsByShop = cartItems
            .GroupBy(ci =>
            {
                var variant = variants[ci.ProductVariantId];
                var product = products[variant.ProductId];
                return product.ShopId;
            })
            .ToList();

        // 12. Create SellerOrder per shop
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

        // Save to get SellerOrder IDs
        await _context.SaveChangesAsync(cancellationToken);

        // 13. Create OrderRequestItem per cart item + decrement stock
        foreach (var shopGroup in itemsByShop)
        {
            var sellerOrder = sellerOrders.First(so => so.ShopId == shopGroup.Key);

            foreach (var cartItem in shopGroup)
            {
                var variant = variants[cartItem.ProductVariantId];
                var product = products[variant.ProductId];

                // Load variant attributes for snapshot
                var attributes = await _context.VariantAttributeValues
                    .Where(vav => vav.ProductVariantId == variant.Id)
                    .ToListAsync(cancellationToken);

                var attributesDict = attributes
                    .GroupBy(a => a.AttributeName)
                    .ToDictionary(g => g.Key, g => g.First().AttributeValue);

                var attributesJson = JsonSerializer.Serialize(attributesDict);

                // Create OrderRequestItem
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

                // Decrement stock
                var previousQty = variant.StockQty;
                variant.StockQty -= cartItem.Quantity;
                variant.SetUpdatedAt(now);

                // Log InventoryTransaction
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

        // 14. Clear cart
        _context.CartItems.RemoveRange(cartItems);

        // 15. Save all
        await _context.SaveChangesAsync(cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // PART 3 — Auto-create first chat message
        // ═══════════════════════════════════════════════════════════

        // 16. Load items for message
        var orderItemsForMessage = await _context.OrderRequestItems
            .Where(i => i.OrderRequestId == orderRequest.Id)
            .ToListAsync(cancellationToken);

        var messageText = BuildOrderMessage(orderRequest.Id, orderItemsForMessage);

        // 17. Create auto chat message
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

        // 18. Return
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