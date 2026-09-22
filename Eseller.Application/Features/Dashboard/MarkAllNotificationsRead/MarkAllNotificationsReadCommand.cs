using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand : IRequest<Result>;