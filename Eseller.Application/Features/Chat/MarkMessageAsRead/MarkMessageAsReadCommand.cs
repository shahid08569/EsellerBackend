using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.MarkMessageAsRead;

public record MarkMessageAsReadCommand(Guid MessageId) : IRequest<Result>;