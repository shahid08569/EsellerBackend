using Eseller.Application.Abstractions;
using Eseller.Application.Features.Chat;
using Eseller.Application.Features.Chat.GetChatMessages;
using Eseller.Application.Features.Chat.GetConversations;
using Eseller.Application.Features.Chat.GetUnreadCount;
using Eseller.Application.Features.Chat.MarkMessageAsRead;
using Eseller.Application.Features.Chat.SendChatMessage;
using Eseller.Application.Features.Chat.EditChatMessage;
using Eseller.Application.Features.Chat.DeleteChatMessage;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
    private readonly ISignalRNotificationService _signalR;

    public ChatController(
        ISender sender,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService signalR)
    {
        _sender = sender;
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _signalR = signalR;
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

        var conversation = await SupportConversationService.GetOrCreateAsync(
            _context, user.Id, ChatParticipantType.Customer, _dateTime.UtcNow, cancellationToken);

        return Ok(new
        {
            conversationId = conversation.Id,
            orderRequestId = conversation.Id, // legacy alias for Angular
            orderRef = conversation.Id.ToString()
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
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null, cancellationToken);
        if (shop is null)
            return NotFound(new { error = "Shop not found.", errorCode = "SHOP_NOT_FOUND" });

        var conversation = await SupportConversationService.GetOrCreateAsync(
            _context, shop.Id, ChatParticipantType.Seller, _dateTime.UtcNow, cancellationToken);

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

            var inquiryMessage = ProductChatContext.BuildMessage(product.Name, product.BasePrice, product.Id, imageUrl);

            var lastMsg = await _context.Messages
                .Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.SentAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastMsg is null || !lastMsg.Contains(product.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _context.Messages.Add(new Domain.Entities.Chat.Message
                {
                    ConversationId = conversation.Id,
                    SenderId = _currentUserService.AccountId.Value,
                    SenderRole = ChatSenderRole.Shopkeeper,
                    Content = inquiryMessage,
                    SentAt = _dateTime.UtcNow,
                    IsRead = false
                });
                conversation.LastMessageAt = _dateTime.UtcNow;
                conversation.SetUpdatedAt(_dateTime.UtcNow);
                await _context.SaveChangesAsync(cancellationToken);
            }

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
            conversationId = conversation.Id,
            orderRequestId = conversation.Id,
            orderRef = conversation.Id.ToString(),
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
                lastMessage = CleanReplyPreview(lm?.Message),
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
        var command = new SendChatMessageCommand(
            orderId,
            request.Message,
            request.AttachmentUrl,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" or "CONVERSATION_NOT_FOUND" =>
                    NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new
        {
            messageId = result.Value.MessageId,
            isDelivered = result.Value.IsDelivered,
            deliveredAt = result.Value.DeliveredAt,
            message = "Message sent."
        });
    }

    /// <summary>
    /// Edit an existing message in-place (WhatsApp style).
    /// </summary>
    [HttpPut("messages/{messageId:guid}")]
    public async Task<IActionResult> EditMessage(
        Guid messageId,
        [FromBody] EditChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EditChatMessageCommand(messageId, request.Message);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "MESSAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                "MESSAGE_DELETED" => StatusCode(StatusCodes.Status409Conflict, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Delete a message (WhatsApp style: forEveryone = true / false).
    /// </summary>
    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid messageId,
        [FromQuery] bool forEveryone = false,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteChatMessageCommand(messageId, forEveryone);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "MESSAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Batch delete multiple messages (WhatsApp style: forEveryone = true / false).
    /// </summary>
    [HttpPost("messages/batch-delete")]
    public async Task<IActionResult> BatchDeleteMessages(
        [FromBody] BatchDeleteMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new BatchDeleteChatMessagesCommand(request.MessageIds, request.ForEveryone);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Upload a chat attachment (image/pdf/doc). Returns URL to attach on send.
    /// </summary>
    [HttpPost("attachments")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadAttachment(
        IFormFile file,
        [FromServices] IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required.", errorCode = "FILE_REQUIRED" });

        if (file.Length > 10_485_760)
            return BadRequest(new { error = "Max file size is 10 MB.", errorCode = "FILE_TOO_LARGE" });

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif",
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain"
        };

        var extByMime = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".webp"] = ["image/webp"],
            [".gif"] = ["image/gif"],
            [".pdf"] = ["application/pdf"],
            [".doc"] = ["application/msword"],
            [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
            [".xls"] = ["application/vnd.ms-excel"],
            [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
            [".txt"] = ["text/plain"]
        };

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType.Trim();

        // Browsers often send octet-stream / empty — fall back to extension allowlist
        if (!allowed.Contains(contentType))
        {
            if (extByMime.TryGetValue(ext, out var mimes))
                contentType = mimes[0];
            else
                return BadRequest(new { error = "File type not allowed.", errorCode = "FILE_TYPE" });
        }

        if (string.IsNullOrWhiteSpace(ext))
            ext = ".bin";
        var relativeName = $"chat/{_currentUserService.AccountId.Value:N}/{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        var url = await fileStorage.UploadAsync(stream, relativeName, contentType, cancellationToken);

        return Ok(new
        {
            url,
            fileName = Path.GetFileName(file.FileName),
            contentType,
            sizeBytes = file.Length
        });
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
    /// Shop support threads use Shop.Id as the room key — ensure channel exists before marking.
    /// </summary>
    [HttpPut("{orderRequestId:guid}/read")]
    public async Task<IActionResult> MarkConversationAsRead(
        Guid orderRequestId,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Unauthorized(new { error = "User is not authenticated.", errorCode = "UNAUTHENTICATED" });

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == orderRequestId, cancellationToken);

        if (conversation is null)
        {
            var isShop = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(s => s.Id == orderRequestId && s.DeletedAt == null, cancellationToken);
            if (isShop)
            {
                conversation = await SupportConversationService.GetOrCreateAsync(
                    _context, orderRequestId, ChatParticipantType.Seller, _dateTime.UtcNow, cancellationToken);
            }
            else
            {
                var isUser = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == orderRequestId, cancellationToken);
                if (isUser)
                {
                    conversation = await SupportConversationService.GetOrCreateAsync(
                        _context, orderRequestId, ChatParticipantType.Customer, _dateTime.UtcNow, cancellationToken);
                }
            }
        }

        if (conversation is null)
            return NotFound(new { error = "Conversation not found.", errorCode = "ORDER_NOT_FOUND" });

        if (!await SupportConversationService.CanAccessAsync(
                _context, _currentUserService, conversation, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden.", errorCode = "FORBIDDEN" });

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var now = _dateTime.UtcNow;
        var accountId = _currentUserService.AccountId.Value;

        // Collect all related room IDs (conversation ID, participant ID, order request ID)
        var roomIds = new HashSet<Guid> { orderRequestId, conversation.Id };
        if (conversation.ParticipantId != Guid.Empty)
        {
            roomIds.Add(conversation.ParticipantId);
        }

        if (role == RoleType.Shopkeeper)
        {
            var shopkeeper = await _context.Shopkeepers.AsNoTracking()
                .FirstOrDefaultAsync(sk => sk.AccountId == accountId, cancellationToken);
            if (shopkeeper != null)
            {
                var shopIds = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
                    .Select(s => s.Id)
                    .ToListAsync(cancellationToken);
                foreach (var sid in shopIds)
                {
                    roomIds.Add(sid);
                }
                var relatedConvoIds = await _context.Conversations.AsNoTracking()
                    .Where(c => c.ParticipantType == ChatParticipantType.Seller && shopIds.Contains(c.ParticipantId))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                foreach (var cid in relatedConvoIds)
                {
                    roomIds.Add(cid);
                }
            }
        }
        else if (role == RoleType.User)
        {
            var userOrders = await _context.OrderRequests.AsNoTracking()
                .Where(o => o.UserId == conversation.ParticipantId)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            foreach (var oid in userOrders)
            {
                roomIds.Add(oid);
            }
        }

        var roomIdList = roomIds.ToList();

        var query = _context.Messages
            .Where(m => roomIdList.Contains(m.ConversationId)
                        && !m.IsRead
                        && m.SenderId != accountId);

        if (isAdmin)
        {
            query = query.Where(m =>
                m.SenderRole != ChatSenderRole.SuperAdmin && m.SenderRole != ChatSenderRole.Partner);
        }
        else
        {
            query = query.Where(m =>
                m.SenderRole != ChatSenderRole.Shopkeeper);
        }

        var unread = await query.ToListAsync(cancellationToken);
        foreach (var msg in unread)
        {
            msg.IsRead = true;
            msg.ReadAt = now;
            if (msg.DeliveredAt == null)
            {
                msg.DeliveredAt = now;
            }
        }

        // Also mark any legacy ChatMessages as read
        var legacyQuery = _context.ChatMessages
            .Where(cm => roomIdList.Contains(cm.OrderRequestId)
                        && !cm.IsRead
                        && cm.SenderAccountId != accountId);

        if (isAdmin)
        {
            legacyQuery = legacyQuery.Where(cm =>
                cm.SenderRole != ChatSenderRole.SuperAdmin && cm.SenderRole != ChatSenderRole.Partner);
        }
        else
        {
            legacyQuery = legacyQuery.Where(cm =>
                cm.SenderRole != ChatSenderRole.Shopkeeper);
        }

        var legacyUnread = await legacyQuery.ToListAsync(cancellationToken);
        foreach (var cm in legacyUnread)
        {
            cm.IsRead = true;
            cm.ReadAt = now;
            if (cm.DeliveredAt == null)
            {
                cm.DeliveredAt = now;
            }
        }

        // Clear bell notifications for this conversation so they disappear after opening/reading
        var chatNotifs = await _context.Notifications
            .Where(n => n.RecipientUserId == accountId
                        && !n.IsRead
                        && n.Type == NotificationType.NewMessage
                        && n.EntityType == NotificationEntityType.Conversation
                        && n.EntityId.HasValue
                        && roomIdList.Contains(n.EntityId.Value))
            .ToListAsync(cancellationToken);

        foreach (var n in chatNotifs)
        {
            n.IsRead = true;
            n.SetUpdatedAt(now);
        }

        if (unread.Count > 0 || legacyUnread.Count > 0 || chatNotifs.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        if (unread.Count > 0)
        {
            try
            {
                foreach (var msg in unread)
                {
                    await _signalR.BroadcastMessageReadAsync(
                        conversation.Id,
                        msg.Id,
                        msg.ReadAt ?? now,
                        cancellationToken,
                        notifyAccountIds: new[] { msg.SenderId });
                }
            }
            catch
            {
                // best-effort realtime
            }
        }

        if (chatNotifs.Count > 0)
        {
            try
            {
                var remaining = await _context.Notifications.AsNoTracking()
                    .CountAsync(n => n.RecipientUserId == accountId && !n.IsRead, cancellationToken);
                await _signalR.SendUnreadCountChangedAsync(accountId, remaining, cancellationToken);
            }
            catch
            {
                // best-effort realtime
            }
        }

        return Ok(new
        {
            message = "Conversation marked as read.",
            markedCount = unread.Count,
            notificationsCleared = chatNotifs.Count
        });
    }

    /// <summary>
    /// Get all conversations (admin only).
    /// </summary>
    [HttpGet("conversations")]
    [Authorize(Roles = "SuperAdmin,Partner")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 500,
        CancellationToken cancellationToken = default)
    {
        try
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client navigated away / refresh aborted the request — not a fault
            return NoContent();
        }
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

    private static readonly Regex ReplyTagRegex = new(
        @"^\[reply:[^:]+:[^\]]+\][\s\S]*?\[/reply\]\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string? CleanReplyPreview(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var cleaned = ReplyTagRegex.Replace(raw, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            var match = Regex.Match(raw, @"^\[reply:[^:]+:([^\]]+)\]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var sender = match.Groups[1].Value.Trim();
                return !string.IsNullOrWhiteSpace(sender) ? $"Replied to {sender}" : "Reply";
            }
            return "Reply";
        }
        return cleaned;
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record SendMessageRequest(
    string? Message,
    string? AttachmentUrl = null,
    string? AttachmentFileName = null,
    string? AttachmentContentType = null,
    long? AttachmentSizeBytes = null);

public record EditChatMessageRequest(string Message);

public record BatchDeleteMessagesRequest(
    List<Guid> MessageIds,
    bool ForEveryone = false);