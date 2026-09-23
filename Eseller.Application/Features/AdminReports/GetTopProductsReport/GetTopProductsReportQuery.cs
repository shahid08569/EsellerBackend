using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminReports.GetTopProductsReport;

public record GetTopProductsReportQuery(
    DateTime FromDate,
    DateTime ToDate,
    int Limit = 10) : IRequest<Result<TopProductsReportDto>>;