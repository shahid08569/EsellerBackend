using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.EditChatMessage;

public record EditChatMessageCommand(
    Guid MessageId,
    string NewContent) : IRequest<Result<EditChatMessageResult>>;

public record EditChatMessageResult(
    Guid MessageId,
    Guid ConversationId,
    string Content,
    bool IsEdited,
    DateTime EditedAt);
