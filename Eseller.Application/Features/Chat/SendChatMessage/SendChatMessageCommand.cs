using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.SendChatMessage;

public record SendChatMessageCommand(
    Guid OrderRequestId,
    string Message) : IRequest<Result<Guid>>;