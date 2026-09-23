using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminReports.GetSalesReport;

public record GetSalesReportQuery(
    DateTime FromDate,
    DateTime ToDate) : IRequest<Result<SalesReportDto>>;