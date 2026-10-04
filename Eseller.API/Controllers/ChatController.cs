using Eseller.Application.Abstractions;
using Eseller.Application.Features.Chat;
using Eseller.Application.Features.Chat.GetChatMessages;
using Eseller.Application.Features.Chat.GetConversations;
using Eseller.Application.Features.Chat.GetUnreadCount;
using Eseller.Application.Features.Chat.MarkMessageAsRead;
using Eseller.Application.Features.Chat.SendChatMessage;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ChatController(
        ISender sender,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _sender = sender;
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    /// <summary>
    /// Disabled — customers chat with Super Admin only (not sellers).
    /// </summary>
    [HttpGet("shop-session")]
    public IActionResult GetOrCreateShopSession()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            error = "Customer–seller chat is disabled. Please use platform support to chat with Super Admin.",
            errorCode = "CHAT_DISABLED"
        });
    }

    /// <summary>
    /// Customer: dedicated platform support channel (does NOT reuse shop/order threads).
    /// Marker: CUSTOMER_SUPPORT_{userId}
    /// </summary>
    [HttpGet("support-session")]
    public async Task<IActionResult> GetOrCreateSupportSession(CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value, cancellationToken);

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

        var marker = $"CUSTOMER_SUPPORT_{user.Id}";
        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.UserId == user.Id && o.ShippingAddress == marker, cancellationToken);

        if (order is null)
        {
            order = new OrderRequest
            {
                UserId = user.Id,
                CustomerName = user.Name,
                CustomerPhone = user.Phone ?? "N/A",
                ShippingAddress = marker,
                City = "Online",
                OrderNotes = "Customer ↔ Platform support channel",
                Status = OrderStatus.Pending
            };
            order.SetCreatedAt(_dateTime.UtcNow);
            order.SetUpdatedAt(_dateTime.UtcNow);
            _context.OrderRequests.Add(order);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            orderRequestId = order.Id,
            orderRef = order.Id.ToString()
        });
    }

    /// <summary>
    /// Seller: open Super Admin support chat, optionally scoped to a product inquiry.
    /// Room id = Shop.Id (SHOP_SUPPORT_*). Product details are attached for the admin feed.
    /// </summary>
    [HttpGet("shop-support-session")]
    [Authorize(Roles = "Shopkeeper")]
    public async Task<IActionResult> GetOrCreateShopSupportSession(
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized();

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value, cancellationToken);
        if (shopkeeper is null)
            return NotFound(new { error = "Shopkeeper not found.", errorCode = "SHOPKEEPER_NOT_FOUND" });

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null, cancellationToken);
        if (shop is null)
            return NotFound(new { error = "Shop not found.", errorCode = "SHOP_NOT_FOUND" });

        var channel = await ShopSupportChannel.EnsureAsync(
            _context, shop.Id, _dateTime.UtcNow, cancellationToken);
        if (channel is null)
            return BadRequest(new { error = "Could not open support channel.", errorCode = "CHANNEL_FAILED" });

        object? productPayload = null;

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == productId.Value
                    && p.ShopId == shop.Id
                    && p.DeletedAt == null, cancellationToken);

            if (product is null)
                return NotFound(new { error = "Product not found in your shop.", errorCode = "PRODUCT_NOT_FOUND" });

            var imageUrl = await _context.ProductImages
                .AsNoTracking()
                .Where(i => i.ProductId == product.Id)
                .OrderByDescending(i => i.IsCover)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .FirstOrDefaultAsync(cancellationToken);

            // Persist product context for admin inbox feed
            channel.OrderNotes = ProductChatContext.Encode(product.Id, product.Name, product.BasePrice, imageUrl);
            channel.SetUpdatedAt(_dateTime.UtcNow);

            var inquiryMessage = ProductChatContext.BuildMessage(product.Name, product.BasePrice, product.Id, imageUrl);

            // Avoid spamming identical inquiry if last message is the same product
            var lastMsg = await _context.ChatMessages
                .Where(m => m.OrderRequestId == channel.Id)
                .OrderByDescending(m => m.SentAt)
                .Select(m => m.Message)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastMsg is null || !lastMsg.Contains(product.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var chatMsg = new ChatMessage
                {
                    OrderRequestId = channel.Id,
                    SenderAccountId = _currentUserService.AccountId.Value,
                    SenderRole = ChatSenderRole.Shopkeeper,
                    Message = inquiryMessage,
                    SentAt = _dateTime.UtcNow,
                    IsRead = false
                };
                _context.ChatMessages.Add(chatMsg);
            }

            await _context.SaveChangesAsync(cancellationToken);

            productPayload = new
            {
                id = product.Id,
                name = product.Name,
                price = product.BasePrice,
                imageUrl
            };
        }

        return Ok(new
        {
            orderRequestId = channel.Id,
            orderRef = channel.Id.ToString(),
            shopId = shop.Id,
            shopName = shop.Name,
            product = productPayload
        });
    }

    /// <summary>
    /// Customer: WhatsApp-style inbox — separate history per shop + order chats with messages.
    /// Shop threads use marker SHOP_CHAT_{shopId}_{userId}.
    /// </summary>
    [HttpGet("my-conversations")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetMyConversations(CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value, cancellationToken);

        if (user is null)
            return Ok(Array.Empty<object>());

        const string shopPrefix = "SHOP_CHAT_";
        var supportMarker = $"CUSTOMER_SUPPORT_{user.Id}";

        var channelOrders = await _context.OrderRequests
            .AsNoTracking()
            .Where(o => o.UserId == user.Id
                && o.ShippingAddress != null
                && (o.ShippingAddress.StartsWith(shopPrefix) || o.ShippingAddress == supportMarker))
            .OrderByDescending(o => o.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var commerceOrders = await _context.OrderRequests
            .AsNoTracking()
            .Where(o => o.UserId == user.Id
                && (o.ShippingAddress == null
                    || (!o.ShippingAddress.StartsWith(shopPrefix) && o.ShippingAddress != supportMarker))
                && _context.ChatMessages.Any(m => m.OrderRequestId == o.Id))
            .OrderByDescending(o => o.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var allOrders = channelOrders.Concat(commerceOrders).GroupBy(o => o.Id).Select(g => g.First()).ToList();
        var ids = allOrders.Select(o => o.Id).ToList();

        var lastMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => ids.Contains(m.OrderRequestId))
            .GroupBy(m => m.OrderRequestId)
            .Select(g => new
            {
                OrderRequestId = g.Key,
                Message = g.OrderByDescending(x => x.SentAt).Select(x => x.Message).FirstOrDefault(),
                SentAt = g.Max(x => x.SentAt),
                Unread = g.Count(x => !x.IsRead && x.SenderRole != ChatSenderRole.User)
            })
            .ToListAsync(cancellationToken);

        var lastByOrder = lastMessages.ToDictionary(x => x.OrderRequestId);

        // Resolve shop ids from SHOP_CHAT_{shopId}_{userId}
        var shopIdByOrder = new Dictionary<Guid, Guid>();
        foreach (var o in channelOrders)
        {
            if (o.ShippingAddress is null || !o.ShippingAddress.StartsWith(shopPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var rest = o.ShippingAddress[shopPrefix.Length..];
            var underscore = rest.IndexOf('_');
            if (underscore > 0 && Guid.TryParse(rest[..underscore], out var sid))
                shopIdByOrder[o.Id] = sid;
        }

        var sellerShopByOrder = await _context.SellerOrders
            .AsNoTracking()
            .Where(so => ids.Contains(so.OrderRequestId))
            .Select(so => new { so.OrderRequestId, so.ShopId })
            .ToListAsync(cancellationToken);

        foreach (var so in sellerShopByOrder)
            shopIdByOrder.TryAdd(so.OrderRequestId, so.ShopId);

        var shopIds = shopIdByOrder.Values.Distinct().ToList();
        var shopNames = await _context.Shops
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => shopIds.Contains(s.Id) && s.DeletedAt == null)
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var result = allOrders.Select(o =>
        {
            lastByOrder.TryGetValue(o.Id, out var lm);
            shopIdByOrder.TryGetValue(o.Id, out var sid);
            var isSupport = o.ShippingAddress == supportMarker;
            var isShop = o.ShippingAddress != null
                && o.ShippingAddress.StartsWith(shopPrefix, StringComparison.OrdinalIgnoreCase);
            var threadType = isSupport ? "Support" : isShop ? "Shop" : "Order";
            var shopName = isSupport
                ? "Eseller Support"
                : (sid != Guid.Empty && shopNames.TryGetValue(sid, out var n) ? n : "Seller");

            return new
            {
                orderRequestId = o.Id,
                threadType,
                shopId = sid == Guid.Empty ? (Guid?)null : sid,
                shopName,
                lastMessage = lm?.Message,
                lastMessageTime = lm?.SentAt ?? o.UpdatedAt,
                unreadCount = lm?.Unread ?? 0
            };
        })
        .OrderByDescending(t => t.lastMessageTime)
        .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get chat messages for an order.
    /// </summary>
    [HttpGet("{orderId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid orderId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetChatMessagesQuery(orderId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Send a message to an order chat.
    /// </summary>
    [HttpPost("{orderId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid orderId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendChatMessageCommand(orderId, request.Message);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { messageId = result.Value, message = "Message sent." });
    }

    /// <summary>
    /// Mark message as read.
    /// </summary>
    [HttpPut("messages/{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var command = new MarkMessageAsReadCommand(messageId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "MESSAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Message marked as read." });
    }

    /// <summary>
    /// Mark all unread inbound messages in a conversation/thread as read.
    /// </summary>
    [HttpPut("{orderRequestId:guid}/read")]
    public async Task<IActionResult> MarkConversationAsRead(
        Guid orderRequestId,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized(new { error = "User is not authenticated.", errorCode = "UNAUTHENTICATED" });

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == orderRequestId, cancellationToken);
        if (order is null)
            return NotFound(new { error = "Conversation not found.", errorCode = "ORDER_NOT_FOUND" });

        var unread = await _context.ChatMessages
            .Where(cm => cm.OrderRequestId == orderRequestId
                      && !cm.IsRead
                      && cm.SenderAccountId != _currentUserService.AccountId.Value)
            .ToListAsync(cancellationToken);

        var marked = 0;
        foreach (var msg in unread)
        {
            var mark = await _sender.Send(new MarkMessageAsReadCommand(msg.Id), cancellationToken);
            if (!mark.IsSuccess && mark.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden, new { error = mark.Error, errorCode = mark.ErrorCode });
            if (mark.IsSuccess) marked++;
        }

        return Ok(new { message = "Conversation marked as read.", markedCount = marked });
    }

    /// <summary>
    /// Get all conversations (admin only).
    /// </summary>
    [HttpGet("conversations")]
    [Authorize(Roles = "SuperAdmin,Partner")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetConversationsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get unread message count.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetUnreadCountQuery();
            var result = await _sender.Send(query, cancellationToken);

            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

            return Ok(new { unreadCount = result.Value });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Poll aborted (navigation / refresh) — ignore
            return Ok(new { unreadCount = 0 });
        }
    }

    /// <summary>
    /// Disabled — sellers chat with Super Admin only (not customers).
    /// </summary>
    [HttpGet("shop-conversations")]
    [Authorize(Roles = "Shopkeeper")]
    public IActionResult GetShopConversations()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            error = "Seller–customer chat is disabled. Use shop support to chat with Super Admin.",
            errorCode = "CHAT_DISABLED"
        });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record SendMessageRequest(string Message);