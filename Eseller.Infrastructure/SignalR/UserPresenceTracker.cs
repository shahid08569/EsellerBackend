using System.Collections.Concurrent;
using Eseller.Application.Abstractions.SignalR;

namespace Eseller.Infrastructure.SignalR;

public class UserPresenceTracker : IUserPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _onlineUsers = new();

    public void TrackUserConnected(Guid accountId, string connectionId)
    {
        if (accountId == Guid.Empty) return;
        var connections = _onlineUsers.GetOrAdd(accountId, _ => new ConcurrentDictionary<string, byte>());
        connections.TryAdd(connectionId, 0);
    }

    public void TrackUserDisconnected(Guid accountId, string connectionId)
    {
        if (accountId == Guid.Empty) return;
        if (_onlineUsers.TryGetValue(accountId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
            {
                _onlineUsers.TryRemove(accountId, out _);
            }
        }
    }

    public bool IsUserOnline(Guid accountId)
    {
        if (accountId == Guid.Empty) return false;
        return _onlineUsers.TryGetValue(accountId, out var connections) && !connections.IsEmpty;
    }

    public bool IsAnyUserOnline(IEnumerable<Guid> accountIds)
    {
        if (accountIds == null) return false;
        return accountIds.Any(IsUserOnline);
    }
}
