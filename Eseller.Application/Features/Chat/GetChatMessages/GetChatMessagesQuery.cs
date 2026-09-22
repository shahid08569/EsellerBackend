using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.GetChatMessages;

public record GetChatMessagesQuery(
    Guid OrderRequestId,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedList<ChatMessageDto>>>;