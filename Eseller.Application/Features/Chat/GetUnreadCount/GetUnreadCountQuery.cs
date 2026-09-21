using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.GetUnreadCount;

public record GetUnreadCountQuery : IRequest<Result<int>>;