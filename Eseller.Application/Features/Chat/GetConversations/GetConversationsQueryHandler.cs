using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopkeeperApprovalStatus = Eseller.Domain.Enums.ShopkeeperApprovalStatus;

namespace Eseller.Application.Features.Chat.GetConversations;

public class GetConversationsQueryHandler
    : IRequestHandler<GetConversationsQuery, Result<PagedList<ConversationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ConversationDto>>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ConversationDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<ConversationDto>>(
                "Only admins can view conversations.", "FORBIDDEN");

        var conversations = new List<ConversationDto>();

        // ── 1) Every seller shop = WhatsApp-style thread with Super Admin ──
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var shopIds = shops.Select(s => s.Id).ToList();
        var shopkeepers = await _context.Shopkeepers
            .Where(sk => shops.Select(s => s.ShopkeeperId).Contains(sk.Id))
            .ToDictionaryAsync(sk => sk.Id, cancellationToken);

        var shopkeeperAccountIds = shopkeepers.Values.Select(sk => sk.AccountId).Distinct().ToList();
        var shopAccounts = await _context.Accounts
            .Where(a => shopkeeperAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var supportMessages = shopIds.Count == 0
            ? new List<Domain.Entities.Orders.ChatMessage>()
            : await _context.ChatMessages
                .Where(cm => shopIds.Contains(cm.OrderRequestId))
                .OrderByDescending(cm => cm.SentAt)
                .ToListAsync(cancellationToken);

        var supportByShop = supportMessages
            .GroupBy(m => m.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var supportOrders = shopIds.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Orders.OrderRequest>()
            : await _context.OrderRequests
                .AsNoTracking()
                .Where(o => shopIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

        foreach (var shop in shops)
        {
            shopkeepers.TryGetValue(shop.ShopkeeperId, out var keeper);
            if (keeper is not null && keeper.Status == ShopkeeperApprovalStatus.Rejected)
                continue;

            var messages = supportByShop.GetValueOrDefault(shop.Id, []);
            var last = messages.FirstOrDefault();
            var unread = messages.Count(m =>
                !m.IsRead && m.SenderRole == ChatSenderRole.Shopkeeper);

            var statusLabel = keeper is null
                ? "Merchant Support"
                : keeper.Status == ShopkeeperApprovalStatus.Approved
                    ? "Verified Merchant"
                    : "Pending Verification";

            var email = keeper is not null && shopAccounts.TryGetValue(keeper.AccountId, out var acc)
                ? acc.Email
                : string.Empty;

            string? ctxId = null, ctxName = null, ctxImg = null;
            decimal? ctxPrice = null;
            if (supportOrders.TryGetValue(shop.Id, out var supportOrder)
                && ProductChatContext.TryParse(supportOrder.OrderNotes, out var pid, out var pname, out var pprice, out var pimg))
            {
                ctxId = pid.ToString();
                ctxName = pname;
                ctxPrice = pprice;
                ctxImg = pimg;
            }

            conversations.Add(new ConversationDto(
                shop.Id,
                keeper?.Id ?? Guid.Empty,
                shop.Name,
                email,
                statusLabel,
                0,
                0,
                last?.Message ?? "Direct support channel",
                last?.SenderAccountId,
                last?.SentAt,
                unread,
                last?.SentAt ?? shop.CreatedAt,
                ctxId,
                ctxName,
                ctxImg,
                ctxPrice));
        }

        // ── 2) Customer ↔ Super Admin support channels only (no seller/customer shop chat) ──
        var orderIdsWithChat = await _context.ChatMessages
            .Select(cm => cm.OrderRequestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Drop ones already represented as shop support threads
        var shopIdSet = shopIds.ToHashSet();
        orderIdsWithChat = orderIdsWithChat.Where(id => !shopIdSet.Contains(id)).ToList();

        if (orderIdsWithChat.Count > 0)
        {
            var orders = await _context.OrderRequests
                .Where(o => orderIdsWithChat.Contains(o.Id))
                .ToListAsync(cancellationToken);

            // Only CUSTOMER_SUPPORT_* (and legacy live-support marker)
            orders = orders
                .Where(o => !string.IsNullOrWhiteSpace(o.ShippingAddress)
                    && (o.ShippingAddress.StartsWith("CUSTOMER_SUPPORT_", StringComparison.OrdinalIgnoreCase)
                        || o.ShippingAddress.Equals("Live Support Inquiry", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);

            var accountIds = users.Values.Select(u => u.AccountId).Distinct().ToList();
            var accounts = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            var orderIdList = orders.Select(o => o.Id).ToList();
            var allItems = await _context.OrderRequestItems
                .Where(i => orderIdList.Contains(i.OrderRequestId))
                .ToListAsync(cancellationToken);
            var itemsByOrder = allItems.GroupBy(i => i.OrderRequestId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var customerMessages = await _context.ChatMessages
                .Where(cm => orderIdList.Contains(cm.OrderRequestId))
                .OrderByDescending(cm => cm.SentAt)
                .ToListAsync(cancellationToken);
            var messagesByOrder = customerMessages.GroupBy(cm => cm.OrderRequestId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var order in orders)
            {
                var user = users.GetValueOrDefault(order.UserId);
                var account = user is null ? null : accounts.GetValueOrDefault(user.AccountId);
                var orderItems = itemsByOrder.GetValueOrDefault(order.Id, []);
                var messages = messagesByOrder.GetValueOrDefault(order.Id, []);
                var lastMessage = messages.FirstOrDefault();
                var unreadCount = messages.Count(m =>
                    !m.IsRead && m.SenderRole == ChatSenderRole.User);

                var displayName = !string.IsNullOrWhiteSpace(user?.Name)
                    ? user.Name
                    : (!string.IsNullOrWhiteSpace(order.CustomerName) ? order.CustomerName : "Customer");

                conversations.Add(new ConversationDto(
                    order.Id,
                    order.UserId,
                    displayName,
                    account?.Email ?? string.Empty,
                    order.Status.ToString(),
                    orderItems.Sum(i => i.TotalPriceSnapshot),
                    orderItems.Sum(i => i.Quantity),
                    lastMessage?.Message,
                    lastMessage?.SenderAccountId,
                    lastMessage?.SentAt,
                    unreadCount,
                    lastMessage?.SentAt ?? order.CreatedAt));
            }
        }

        var sorted = conversations
            .OrderByDescending(c => c.LastMessageAt ?? c.OrderCreatedAt)
            .ToList();

        var totalCount = sorted.Count;
        var page = sorted
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result.Success(new PagedList<ConversationDto>(
            page, totalCount, request.PageNumber, request.PageSize));
    }
}
