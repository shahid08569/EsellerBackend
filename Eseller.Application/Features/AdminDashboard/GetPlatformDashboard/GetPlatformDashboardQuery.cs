using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminDashboard.GetPlatformDashboard;

public record GetPlatformDashboardQuery : IRequest<Result<PlatformDashboardDto>>;