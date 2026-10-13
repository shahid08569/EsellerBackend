namespace Eseller.Application.Abstractions.SignalR;

public interface IUserPresenceTracker
{
    void TrackUserConnected(Guid accountId, string connectionId);
    void TrackUserDisconnected(Guid accountId, string connectionId);
    bool IsUserOnline(Guid accountId);
    bool IsAnyUserOnline(IEnumerable<Guid> accountIds);
}
