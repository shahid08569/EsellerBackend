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
        var isPartner = _currentUserService.RoleType == RoleType.Partner;

        // Load all partners & accounts for referral metadata resolution
        var allPartners = await _context.Partners.AsNoTracking().ToListAsync(cancellationToken);
        var partnerAccountIds = allPartners.Select(p => p.AccountId).Distinct().ToList();
        var partnerAccounts = await _context.Accounts.AsNoTracking()
            .Where(a => partnerAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var partnerMap = allPartners.ToDictionary(p => p.Id);

        string GetPartnerName(Guid? partnerId)
        {
            if (partnerId.HasValue && partnerMap.TryGetValue(partnerId.Value, out var p))
            {
                if (partnerAccounts.TryGetValue(p.AccountId, out var acc))
                {
                    return !string.IsNullOrWhiteSpace(acc.Username) ? acc.Username : acc.Email;
                }
                return p.ReferralCode;
            }
            return "Partner";
        }

        Partner? currentPartner = null;
        if (isPartner && _currentUserService.AccountId.HasValue)
        {
            currentPartner = allPartners.FirstOrDefault(p => p.AccountId == _currentUserService.AccountId.Value);
        }

        // Seller directory: Shop.Id is the conversation room key (created lazily on first message)
        // Skip Platform Master Warehouse — not a real merchant chat target
        var shopsQuery = _context.Shops
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null
                        && s.Slug != MasterCatalog.ShopSlug
                        && s.Name != MasterCatalog.ShopName);

        HashSet<Guid>? partnerShopIds = null;
        if (isPartner)
        {
            partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            if (partnerShopIds != null && partnerShopIds.Count > 0)
            {
                shopsQuery = shopsQuery.Where(s => partnerShopIds.Contains(s.Id));
            }
            else
            {
                shopsQuery = shopsQuery.Where(s => false);
            }
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

        // 1. Direct Seller-SuperAdmin convos (ParticipantType == Seller)
        var sellerConvos = await _context.Conversations.AsNoTracking()
            .Where(c => c.ParticipantType == ChatParticipantType.Seller && (shopIds.Contains(c.ParticipantId) || !isPartner))
            .Select(c => new { c.Id, c.ParticipantId })
            .ToListAsync(cancellationToken);

        var sellerConvoByShop = sellerConvos
            .GroupBy(c => c.ParticipantId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // 2. SellerPartner convos (Partner ↔ Referred Shopkeeper)
        var sellerPartnerConvosQuery = _context.Conversations.AsNoTracking()
            .Where(c => c.ParticipantType == ChatParticipantType.SellerPartner);

        if (isPartner)
        {
            if (partnerShopIds != null && partnerShopIds.Count > 0)
            {
                sellerPartnerConvosQuery = sellerPartnerConvosQuery.Where(c => partnerShopIds.Contains(c.ParticipantId));
            }
            else
            {
                sellerPartnerConvosQuery = sellerPartnerConvosQuery.Where(c => false);
            }
        }

        var sellerPartnerConvos = await sellerPartnerConvosQuery.ToListAsync(cancellationToken);
        var sellerPartnerConvoByShop = sellerPartnerConvos
            .GroupBy(c => c.ParticipantId)
            .ToDictionary(g => g.Key, g => g.First());

        // 3. Partner ↔ SuperAdmin direct conversations
        var partnerDirectConvos = await _context.Conversations.AsNoTracking()
            .Where(c => c.ParticipantType == ChatParticipantType.Partner)
            .ToListAsync(cancellationToken);

        var partnerConvoByPartnerId = partnerDirectConvos
            .GroupBy(c => c.ParticipantId)
            .ToDictionary(g => g.Key, g => g.First());

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

        // Aggregate room IDs for message fetching
        var allRoomIds = shopIds
            .Concat(sellerConvos.Select(c => c.Id))
            .Concat(sellerPartnerConvos.Select(c => c.Id))
            .Concat(sellerPartnerConvos.Select(c => c.ParticipantId))
            .Concat(partnerDirectConvos.Select(c => c.Id))
            .Concat(partnerDirectConvos.Select(c => c.ParticipantId))
            .Concat(customerConvoIds.Select(c => c.Id))
            .Concat(customerConvoIds.Select(c => c.ParticipantId))
            .Distinct()
            .ToList();

        if (isPartner && currentPartner != null)
        {
            allRoomIds.Add(currentPartner.Id);
            if (partnerConvoByPartnerId.TryGetValue(currentPartner.Id, out var pc))
                allRoomIds.Add(pc.Id);
        }

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

        var currentAccountId = _currentUserService.AccountId ?? Guid.Empty;
        var unreadByConvo = await _context.Messages.AsNoTracking()
            .Where(m => allRoomIds.Contains(m.ConversationId)
                        && !m.IsRead
                        && m.SenderId != currentAccountId)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

        var shopAccountIds = shopkeepers.Values.Select(sk => sk.AccountId).Distinct().ToList();
        var shopAccounts = await _context.Accounts.AsNoTracking()
            .Where(a => shopAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // A) If PARTNER:
        if (isPartner)
        {
            // 1. Direct Channel with SuperAdmin (Default & Always Available!)
            if (currentPartner != null)
            {
                partnerConvoByPartnerId.TryGetValue(currentPartner.Id, out var partnerConvo);
                var targetRoomId = partnerConvo?.Id ?? currentPartner.Id;
                var associatedRooms = new HashSet<Guid> { currentPartner.Id };
                if (partnerConvo != null)
                    associatedRooms.Add(partnerConvo.Id);

                Eseller.Domain.Entities.Chat.Message? last = null;
                var unread = 0;
                foreach (var rId in associatedRooms)
                {
                    if (lastByConvo.TryGetValue(rId, out var m))
                    {
                        if (last is null || m.SentAt > last.SentAt) last = m;
                    }
                    if (unreadByConvo.TryGetValue(rId, out var cnt)) unread += cnt;
                }

                var preview = last is null
                    ? "Official platform management channel"
                    : string.IsNullOrWhiteSpace(last.Content)
                        ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                        : CleanReplyPreview(last.Content);

                conversations.Add(new ConversationDto(
                    targetRoomId,
                    currentPartner.Id,
                    "Partner Service",
                    "support@platform.local",
                    "Official Support Channel",
                    0,
                    0,
                    preview,
                    last?.SenderId,
                    last?.SentAt,
                    unread,
                    partnerConvo?.CreatedAt ?? currentPartner.CreatedAt,
                    ParticipantType: "Partner",
                    LogoUrl: null,
                    IsReferred: false,
                    PartnerId: null,
                    PartnerName: null,
                    IsReadOnly: false));
            }

            // 2. Referred Shops for Partner (SellerPartner)
            foreach (var shop in shops)
            {
                shopkeepers.TryGetValue(shop.ShopkeeperId, out var keeper);
                if (keeper is not null && keeper.Status == ShopkeeperApprovalStatus.Rejected)
                    continue;

                sellerPartnerConvoByShop.TryGetValue(shop.Id, out var spConvo);
                var targetRoomId = spConvo?.Id ?? shop.Id;
                var associatedRooms = new HashSet<Guid> { shop.Id };
                if (spConvo != null)
                    associatedRooms.Add(spConvo.Id);

                Eseller.Domain.Entities.Chat.Message? last = null;
                var unread = 0;
                foreach (var rId in associatedRooms)
                {
                    if (lastByConvo.TryGetValue(rId, out var m))
                    {
                        if (last is null || m.SentAt > last.SentAt) last = m;
                    }
                    if (unreadByConvo.TryGetValue(rId, out var cnt)) unread += cnt;
                }

                var email = keeper is not null && shopAccounts.TryGetValue(keeper.AccountId, out var acc)
                    ? acc.Email
                    : string.Empty;

                var preview = last is null
                    ? "Direct partner channel"
                    : string.IsNullOrWhiteSpace(last.Content)
                        ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                        : CleanReplyPreview(last.Content);

                conversations.Add(new ConversationDto(
                    targetRoomId,
                    keeper?.Id ?? Guid.Empty,
                    shop.Name,
                    email,
                    "Referred Merchant",
                    0,
                    0,
                    preview,
                    last?.SenderId,
                    last?.SentAt,
                    unread,
                    last?.SentAt ?? shop.CreatedAt,
                    ParticipantType: "SellerPartner",
                    LogoUrl: shop.LogoUrl,
                    IsReferred: true,
                    PartnerId: currentPartner?.Id,
                    PartnerName: currentPartner?.ReferralCode,
                    IsReadOnly: false,
                    ShopId: shop.Id));
            }
        }
        else
        {
            // B) If SUPERADMIN:
            // 1. Direct Sellers (Shop.PartnerId == null) & Partner Referrals (Shop.PartnerId != null)
            foreach (var shop in shops)
            {
                shopkeepers.TryGetValue(shop.ShopkeeperId, out var keeper);
                if (keeper is not null && keeper.Status == ShopkeeperApprovalStatus.Rejected)
                    continue;

                var targetRoomId = sellerConvoByShop.TryGetValue(shop.Id, out var sConvoId) ? sConvoId : shop.Id;
                var shopAssociatedRooms = new HashSet<Guid> { shop.Id };
                if (sConvoId != Guid.Empty)
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

                var partnerId = shop.PartnerId ?? keeper?.PartnerId;
                var isReferred = partnerId.HasValue;
                var partnerName = isReferred ? GetPartnerName(partnerId) : null;

                var statusLabel = isReferred
                    ? $"Referred by {partnerName}"
                    : (keeper is null
                        ? "Merchant Support"
                        : keeper.Status == ShopkeeperApprovalStatus.Approved
                            ? "Verified Merchant"
                            : "Pending Verification");

                var email = keeper is not null && shopAccounts.TryGetValue(keeper.AccountId, out var acc)
                    ? acc.Email
                    : string.Empty;

                var preview = last is null
                    ? (isReferred ? $"Partner referral support ({partnerName})" : "Direct support channel")
                    : string.IsNullOrWhiteSpace(last.Content)
                        ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                        : CleanReplyPreview(last.Content);

                conversations.Add(new ConversationDto(
                    targetRoomId,
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
                    LogoUrl: shop.LogoUrl,
                    IsReferred: isReferred,
                    PartnerId: partnerId,
                    PartnerName: partnerName,
                    IsReadOnly: false,
                    ShopId: shop.Id));
            }

            // 2. Partner's Sellers (Audit view for SuperAdmin — STRICTLY READ ONLY!)
            foreach (var spConvo in sellerPartnerConvos)
            {
                var shop = shops.FirstOrDefault(s => s.Id == spConvo.ParticipantId);
                if (shop is null) continue;

                shopkeepers.TryGetValue(shop.ShopkeeperId, out var keeper);
                var partnerId = shop.PartnerId ?? keeper?.PartnerId;
                var partnerName = GetPartnerName(partnerId);

                lastByConvo.TryGetValue(spConvo.Id, out var last);
                if (last is null && spConvo.ParticipantId != spConvo.Id)
                    lastByConvo.TryGetValue(spConvo.ParticipantId, out last);

                // Unread for audit is not tracked as actionable inbox for SuperAdmin
                var preview = last is null
                    ? $"Partner ({partnerName}) chat with {shop.Name}"
                    : string.IsNullOrWhiteSpace(last.Content)
                        ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                        : CleanReplyPreview(last.Content);

                var email = keeper is not null && shopAccounts.TryGetValue(keeper.AccountId, out var acc)
                    ? acc.Email
                    : string.Empty;

                conversations.Add(new ConversationDto(
                    spConvo.Id,
                    shop.Id,
                    $"{shop.Name} (Partner: {partnerName})",
                    email,
                    $"Partner's Merchant ({partnerName})",
                    0,
                    0,
                    preview,
                    last?.SenderId,
                    last?.SentAt,
                    0, // read-only audit does not increment actionable unread badge for SuperAdmin
                    spConvo.CreatedAt,
                    ParticipantType: "SellerPartner",
                    LogoUrl: shop.LogoUrl,
                    IsReferred: true,
                    PartnerId: partnerId,
                    PartnerName: partnerName,
                    IsReadOnly: true,
                    ShopId: shop.Id));
            }

            // 3. SuperAdmin ↔ Partner Direct Channels
            foreach (var partner in allPartners)
            {
                partnerAccounts.TryGetValue(partner.AccountId, out var pAcc);
                var pName = !string.IsNullOrWhiteSpace(pAcc?.Username)
                    ? pAcc.Username
                    : (!string.IsNullOrWhiteSpace(pAcc?.Email) ? pAcc.Email : partner.ReferralCode);

                partnerConvoByPartnerId.TryGetValue(partner.Id, out var pConvo);
                var targetRoomId = pConvo?.Id ?? partner.Id;
                var associatedRooms = new HashSet<Guid> { partner.Id };
                if (pConvo != null)
                    associatedRooms.Add(pConvo.Id);

                Eseller.Domain.Entities.Chat.Message? last = null;
                var unread = 0;
                foreach (var rId in associatedRooms)
                {
                    if (lastByConvo.TryGetValue(rId, out var m))
                    {
                        if (last is null || m.SentAt > last.SentAt) last = m;
                    }
                    if (unreadByConvo.TryGetValue(rId, out var cnt)) unread += cnt;
                }

                var preview = last is null
                    ? $"Direct channel with Partner {pName}"
                    : string.IsNullOrWhiteSpace(last.Content)
                        ? (last.AttachmentFileName is not null ? $"📎 {last.AttachmentFileName}" : "Attachment")
                        : CleanReplyPreview(last.Content);

                conversations.Add(new ConversationDto(
                    targetRoomId,
                    partner.Id,
                    pName,
                    pAcc?.Email ?? string.Empty,
                    $"Active Partner ({partner.ReferralCode})",
                    0,
                    0,
                    preview,
                    last?.SenderId,
                    last?.SentAt,
                    unread,
                    pConvo?.CreatedAt ?? partner.CreatedAt,
                    ParticipantType: "Partner",
                    LogoUrl: null,
                    IsReferred: false,
                    PartnerId: partner.Id,
                    PartnerName: pName,
                    IsReadOnly: false));
            }
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
