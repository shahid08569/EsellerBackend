namespace Eseller.Application.Abstractions.SignalR;

public interface IChatClient
{
    Task ReceiveMessage(
        Guid messageId,
        Guid orderRequestId,
        Guid senderAccountId,
        string senderRole,
        string message,
        DateTime sentAt);

    Task MessageRead(Guid messageId, DateTime readAt);
}