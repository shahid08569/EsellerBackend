using Eseller.Application.Abstractions;
using Eseller.Application.Abstractions.SignalR;
using Eseller.Application.Features.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Eseller.Infrastructure.SignalR.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserPresenceTracker _presenceTracker;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IUserPresenceTracker presenceTracker,
        ILogger<ChatHub> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var accountId = GetAccountId();
        var role = GetRole();

        _logger.LogInformation(
            "ChatHub connected — Account: {AccountId}, Role: {Role}, ConnectionId: {ConnectionId}",
            accountId, role, Context.ConnectionId);

        if (!string.IsNullOrEmpty(role))
            await Groups.AddToGroupAsync(Context.ConnectionId, role);

        if (accountId != Guid.Empty)
        {
            _presenceTracker.TrackUserConnected(accountId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountId}");
            _ = DeliverPendingMessagesAsync(accountId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var accountId = GetAccountId();
        if (accountId != Guid.Empty)
        {
            _presenceTracker.TrackUserDisconnected(accountId, Context.ConnectionId);
        }

        _logger.LogInformation(
            "ChatHub disconnected — ConnectionId: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    private async Task DeliverPendingMessagesAsync(Guid accountId)
    {
        if (accountId == Guid.Empty) return;

        try
        {
            var now = DateTime.UtcNow;

            var shopIds = await (
                from s in _context.Shops.IgnoreQueryFilters().AsNoTracking()
                join sk in _context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
                where sk.AccountId == accountId && s.DeletedAt == null
                select s.Id
            ).ToListAsync(Context.ConnectionAborted);

            var userIds = await _context.Users.AsNoTracking()
                .Where(u => u.AccountId == accountId)
                .Select(u => u.Id)
                .ToListAsync(Context.ConnectionAborted);

            var participantIds = shopIds.Concat(userIds).Distinct().ToList();

            var conversationIds = await _context.Conversations.AsNoTracking()
                .Where(c => participantIds.Contains(c.ParticipantId) || participantIds.Contains(c.Id) || c.Id == accountId)
                .Select(c => c.Id)
                .ToListAsync(Context.ConnectionAborted);

            var allRoomIds = conversationIds.Concat(participantIds).Distinct().ToList();
            if (allRoomIds.Count == 0) return;

            var undelivered = await _context.Messages
                .Where(m => allRoomIds.Contains(m.ConversationId)
                            && m.SenderId != accountId
                            && m.DeliveredAt == null
                            && !m.IsRead)
                .ToListAsync(Context.ConnectionAborted);

            if (undelivered.Count > 0)
            {
                foreach (var msg in undelivered)
                {
                    msg.DeliveredAt = now;
                }

                await _context.SaveChangesAsync(Context.ConnectionAborted);

                foreach (var msg in undelivered)
                {
                    await Clients.Group($"account-{msg.SenderId}")
                        .MessageDelivered(msg.Id, now);
                    await Clients.Group($"conversation:{msg.ConversationId}")
                        .MessageDelivered(msg.Id, now);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed delivering pending messages for account {AccountId}", accountId);
        }
    }

    /// <summary>Join conversation:{id} (and legacy order-{id}).</summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var conversation = await _context.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId || c.ParticipantId == conversationId, Context.ConnectionAborted);

        if (conversation is null)
        {
            var isShop = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(s => s.Id == conversationId && s.DeletedAt == null, Context.ConnectionAborted);
            if (isShop)
            {
                conversation = await SupportConversationService.GetOrCreateAsync(
                    _context, conversationId, Domain.Enums.ChatParticipantType.Seller, DateTime.UtcNow, Context.ConnectionAborted);
            }
            else
            {
                var isUser = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == conversationId, Context.ConnectionAborted);
                if (isUser)
                {
                    conversation = await SupportConversationService.GetOrCreateAsync(
                        _context, conversationId, Domain.Enums.ChatParticipantType.Customer, DateTime.UtcNow, Context.ConnectionAborted);
                }
                else
                {
                    var order = await _context.OrderRequests.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == conversationId, Context.ConnectionAborted);
                    if (order is not null && order.UserId != Guid.Empty)
                    {
                        conversation = await SupportConversationService.GetOrCreateAsync(
                            _context, order.UserId, Domain.Enums.ChatParticipantType.Customer, DateTime.UtcNow, Context.ConnectionAborted);
                    }
                }
            }
        }

        if (conversation is not null)
        {
            var allowed = await SupportConversationService.CanAccessAsync(
                _context, _currentUser, conversation, Context.ConnectionAborted);

            if (!allowed)
                throw new HubException("Access denied.");

            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversation.Id}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{conversation.Id}");
            if (conversation.ParticipantId != conversation.Id && conversation.ParticipantId != Guid.Empty)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversation.ParticipantId}");
                await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{conversation.ParticipantId}");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{conversationId}");
        _logger.LogInformation(
            "ConnectionId {ConnectionId} joined conversation:{ConversationId}",
            Context.ConnectionId, conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{conversationId}");
    }

    /// <summary>Legacy alias used by existing Angular clients.</summary>
    public Task JoinOrderRoom(Guid orderRequestId) => JoinConversation(orderRequestId);

    public Task LeaveOrderRoom(Guid orderRequestId) => LeaveConversation(orderRequestId);

    private Guid GetAccountId()
    {
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private string? GetRole()
    {
        return Context.User?.FindFirst(ClaimTypes.Role)?.Value;
    }
}
