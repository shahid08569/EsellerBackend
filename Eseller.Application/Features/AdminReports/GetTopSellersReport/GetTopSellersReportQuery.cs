using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminReports.GetTopSellersReport;

public record GetTopSellersReportQuery(
    DateTime FromDate,
    DateTime ToDate,
    int Limit = 10) : IRequest<Result<TopSellersReportDto>>;