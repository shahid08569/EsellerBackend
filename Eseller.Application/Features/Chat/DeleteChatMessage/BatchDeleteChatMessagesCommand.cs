using System;
using System.Collections.Generic;
using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.DeleteChatMessage;

public record BatchDeleteChatMessagesCommand(
    List<Guid> MessageIds,
    bool ForEveryone) : IRequest<Result<BatchDeleteChatMessagesResult>>;

public record BatchDeleteChatMessagesResult(
    int DeletedCount,
    bool ForEveryone);
