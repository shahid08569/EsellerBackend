using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;