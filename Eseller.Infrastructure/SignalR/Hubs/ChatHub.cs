using Eseller.Application.Abstractions.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Eseller.Infrastructure.SignalR.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
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

        // Per-account inbox so admin↔seller / admin↔customer alerts arrive
        // even when the recipient has not joined the order room yet.
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

    public async Task JoinOrderRoom(Guid orderRequestId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderRequestId}");

        _logger.LogInformation(
            "ConnectionId {ConnectionId} joined order-{OrderRequestId}",
            Context.ConnectionId, orderRequestId);
    }

    public async Task LeaveOrderRoom(Guid orderRequestId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderRequestId}");
    }

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