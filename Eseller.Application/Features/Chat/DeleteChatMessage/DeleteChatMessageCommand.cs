using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.DeleteChatMessage;

public record DeleteChatMessageCommand(
    Guid MessageId,
    bool ForEveryone) : IRequest<Result<DeleteChatMessageResult>>;

public record DeleteChatMessageResult(
    Guid MessageId,
    Guid ConversationId,
    bool ForEveryone);
