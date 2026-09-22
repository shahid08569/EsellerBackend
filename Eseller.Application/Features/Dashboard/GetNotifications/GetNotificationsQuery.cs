using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.GetNotifications;

public record GetNotificationsQuery(
    bool? UnreadOnly = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<NotificationDto>>>;