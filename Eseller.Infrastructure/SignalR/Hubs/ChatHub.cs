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
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<ChatHub> logger)
    {
        _context = context;
        _currentUser = currentUser;
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
            await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "ChatHub disconnected — ConnectionId: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Join conversation:{id} (and legacy order-{id}).</summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var conversation = await _context.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, Context.ConnectionAborted);

        if (conversation is null)
            throw new HubException("Conversation not found.");

        var allowed = await SupportConversationService.CanAccessAsync(
            _context, _currentUser, conversation, Context.ConnectionAborted);

        if (!allowed)
            throw new HubException("Access denied.");

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
