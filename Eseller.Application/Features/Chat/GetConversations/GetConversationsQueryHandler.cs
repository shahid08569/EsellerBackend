using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using Eseller.Domain.Entities.Identity;
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

        try
        {
            return await BuildAsync(request, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(new PagedList<ConversationDto>(
                Array.Empty<ConversationDto>(), 0, request.PageNumber, request.PageSize));
        }
        catch (OperationCanceledException)
        {
            return Result.Success(new PagedList<ConversationDto>(
                Array.Empty<ConversationDto>(), 0, request.PageNumber, request.PageSize));
        }
    }

    private async Task<Result<PagedList<ConversationDto>>> BuildAsync(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var conversations = new List<ConversationDto>();

        // Seller directory: Shop.Id is the conversation room key (created lazily on first message)
        // Skip Platform Master Warehouse — not a real merchant chat target
        var shopsQuery = _context.Shops
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null
                        && s.Slug != MasterCatalog.ShopSlug
                        && s.Name != MasterCatalog.ShopName);

        var isPartner = _currentUserService.RoleType == RoleType.Partner;
        HashSet<Guid>? partnerShopIds = null;
        if (isPartner)
        {
            partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            if (partnerShopIds == null || partnerShopIds.Count == 0)
            {
                // Partner has 0 referred shops -> strictly 0 conversations!
                return Result.Success(new PagedList<ConversationDto>(
                    Array.Empty<ConversationDto>(), 0, request.PageNumber, request.PageSize));
            }
            shopsQuery = shopsQuery.Where(s => partnerShopIds.Contains(s.Id));
        }

        var shops = await shopsQuery.ToListAsync(cancellationToken);

        var keeperIds = shops.Select(s => s.ShopkeeperId).Distinct().ToList();
        var shopkeepers = keeperIds.Count == 0
            ? new Dictionary<Guid, Shopkeeper>()
            : await _context.Shopkeepers
                .AsNoTracking()
                .Where(sk => keeperIds.Contains(sk.Id))
                .ToDictionaryAsync(sk => sk.Id, cancellationToken);

        var shopIds = shops.Select(s => s.Id).ToList();

        var sellerConvos = await _context.Conversations.AsNoTracking()
            .Where(c => c.ParticipantType == ChatParticipantType.Seller && (shopIds.Contains(c.ParticipantId) || !isPartner))
            .Select(c => new { c.Id, c.ParticipantId })
            .ToListAsync(cancellationToken);

        var sellerConvoByShop = sellerConvos
            .GroupBy(c => c.ParticipantId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var customerConvoIds = new List<CustomerRoom>();

        // Only SuperAdmin sees platform customer support channels
        if (!isPartner)
        {
            customerConvoIds = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Customer)
                .Select(c => new CustomerRoom(c.Id, c.ParticipantId, c.CreatedAt))
                .ToListAsync(cancellationToken);

            // Legacy customer support orders → room key = User.Id
            var legacyUserIds = await _context.OrderRequests.AsNoTracking()
                .Where(o => o.ShippingAddress != null
                            && (o.ShippingAddress.StartsWith("CUSTOMER_SUPPORT_")
                                || o.ShippingAddress == "Live Support Inquiry")
                            && o.UserId != Guid.Empty)
                .Select(o => o.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var uid in legacyUserIds)
            {
                if (customerConvoIds.All(c => c.ParticipantId != uid))
                    customerConvoIds.Add(new CustomerRoom(uid, uid, DateTime.UtcNow));
            }
        }

        var allRoomIds = shopIds
            .Concat(sellerConvos.Select(c => c.Id))
            .Concat(customerConvoIds.Select(c => c.Id))
            .Concat(customerConvoIds.Select(c => c.ParticipantId))
            .Distinct()
            .ToList();

        // Query max SentAt per ConversationId for all rooms
        var maxDates = await _context.Messages.AsNoTracking()
            .Where(m => allRoomIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, MaxSentAt = g.Max(m => m.SentAt) })
            .ToListAsync(cancellationToken);

        var maxSentAts = maxDates.Select(x => x.MaxSentAt).Distinct().ToList();

        var latestMessages = maxSentAts.Count > 0
            ? await _context.Messages.AsNoTracking()
                .Where(m => allRoomIds.Contains(m.ConversationId) && maxSentAts.Contains(m.SentAt))
                .ToListAsync(cancellationToken)
            : new List<Eseller.Domain.Entities.Chat.Message>();

        var lastByConvo = latestMessages
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SentAt).First());

        var unreadByConvo = await _context.Messages.AsNoTracking()
            .Where(m => allRoomIds.Contains(m.ConversationId)
                        && !m.IsRead
                        && (m.SenderRole == ChatSenderRole.Shopkeeper
                            || m.SenderRole == ChatSenderRole.User))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

        var shopAccountIds = shopkeepers.Values.Select(sk => sk.AccountId).Distinct().ToList();
        var shopAccounts = await _context.Accounts.AsNoTracking()
            .Where(a => shopAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var shop in shops)
        {
            shopkeepers.TryGetValue(shop.ShopkeeperId, out var keeper);
            if (keeper is not null && keeper.Status == ShopkeeperApprovalStatus.Rejected)
                continue;

            var shopAssociatedRooms = new List<Guid> { shop.Id };
            if (sellerConvoByShop.TryGetValue(shop.Id, out var sConvoId) && sConvoId != shop.Id)
                shopAssociatedRooms.Add(sConvoId);

            Eseller.Domain.Entities.Chat.Message? last = null;
            var unread = 0;
            foreach (var rId in shopAssociatedRooms)
            {
                if (lastByConvo.TryGetValue(rId, out var m))
                {
                    if (last is null || m.SentAt > last.SentAt)
                        last = m;
                }
                if (unreadByConvo.TryGetValue(rId, out var cnt))
                {
                    unread += cnt;
                }
            }

            var statusLabel = keeper is null
                ? "Merchant Support"
                : keeper.Status == ShopkeeperApprovalStatus.Approved
                    ? "Verified Merchant"
                    : "Pending Verification";

            var email = keeper is not null && shopAccounts.TryGetValue(keeper.AccountId, out var acc)
                ? acc.Email
                : string.Empty;

            var preview = last is null
                ? "Direct support channel"
                : string.IsNullOrWhiteSpace(last.Content)
                    ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                    : CleanReplyPreview(last.Content);

            conversations.Add(new ConversationDto(
                shop.Id,
                keeper?.Id ?? Guid.Empty,
                shop.Name,
                email,
                statusLabel,
                0,
                0,
                preview,
                last?.SenderId,
                last?.SentAt,
                unread,
                last?.SentAt ?? shop.CreatedAt,
                ParticipantType: "Seller",
                LogoUrl: shop.LogoUrl));
        }

        var userIds = customerConvoIds.Select(c => c.ParticipantId).Distinct().ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        var userAccountIds = users.Values.Select(u => u.AccountId).Distinct().ToList();
        var userAccounts = await _context.Accounts.AsNoTracking()
            .Where(a => userAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var convo in customerConvoIds)
        {
            lastByConvo.TryGetValue(convo.Id, out var last);
            if (last is null && convo.ParticipantId != convo.Id)
                lastByConvo.TryGetValue(convo.ParticipantId, out last);

            unreadByConvo.TryGetValue(convo.Id, out var unread);
            if (convo.ParticipantId != convo.Id && unreadByConvo.TryGetValue(convo.ParticipantId, out var pUnread))
                unread += pUnread;

            if (last is null && unread == 0)
                continue;

            users.TryGetValue(convo.ParticipantId, out var user);
            var account = user is null ? null : userAccounts.GetValueOrDefault(user.AccountId);
            var preview = last is null
                ? null
                : string.IsNullOrWhiteSpace(last.Content)
                    ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                    : CleanReplyPreview(last.Content);

            conversations.Add(new ConversationDto(
                convo.Id,
                convo.ParticipantId,
                !string.IsNullOrWhiteSpace(user?.Name) ? user!.Name : "Customer",
                account?.Email ?? string.Empty,
                "Customer Support",
                0,
                0,
                preview,
                last?.SenderId,
                last?.SentAt,
                unread,
                last?.SentAt ?? convo.CreatedAt,
                ParticipantType: "Customer"));
        }

        var sorted = conversations
            .OrderByDescending(c => c.LastMessageAt ?? c.OrderCreatedAt)
            .ToList();

        var page = sorted
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result.Success(new PagedList<ConversationDto>(
            page, sorted.Count, request.PageNumber, request.PageSize));
    }

    private static readonly System.Text.RegularExpressions.Regex ReplyTagRegex = new(
        @"^\[reply:[^:]+:[^\]]+\][\s\S]*?\[/reply\]\s*",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string? CleanReplyPreview(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var cleaned = ReplyTagRegex.Replace(raw, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            var match = System.Text.RegularExpressions.Regex.Match(raw, @"^\[reply:[^:]+:([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var sender = match.Groups[1].Value.Trim();
                return !string.IsNullOrWhiteSpace(sender) ? $"Replied to {sender}" : "Reply";
            }
            return "Reply";
        }
        return cleaned;
    }

    private sealed record CustomerRoom(Guid Id, Guid ParticipantId, DateTime CreatedAt);
}
