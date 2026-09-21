using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.GetDashboardSummary;

public record GetDashboardSummaryQuery : IRequest<Result<DashboardSummaryDto>>;