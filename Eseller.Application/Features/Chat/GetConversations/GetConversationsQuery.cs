using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.GetConversations;

public record GetConversationsQuery(
    int PageNumber = 1,
    int PageSize = 500) : IRequest<Result<PagedList<ConversationDto>>>;