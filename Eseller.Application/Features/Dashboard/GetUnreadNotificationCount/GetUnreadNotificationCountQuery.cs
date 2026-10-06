using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.GetUnreadNotificationCount;

public record GetUnreadNotificationCountQuery : IRequest<Result<int>>;
