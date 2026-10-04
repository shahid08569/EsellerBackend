using Eseller.Application.Features.AdminReports.GetSalesReport;
using Eseller.Application.Features.AdminReports.GetTopProductsReport;
using Eseller.Application.Features.AdminReports.GetTopSellersReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminReportsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminReportsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Sales report — total orders, revenue, daily breakdown.
    /// </summary>
    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var start = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var end = toDate ?? DateTime.UtcNow;
        var query = new GetSalesReportQuery(start, end);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Top products report — by revenue.
    /// </summary>
    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProductsReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var start = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var end = toDate ?? DateTime.UtcNow;
        var query = new GetTopProductsReportQuery(start, end, limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Top sellers report — by revenue.
    /// </summary>
    [HttpGet("top-sellers")]
    public async Task<IActionResult> GetTopSellersReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var start = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var end = toDate ?? DateTime.UtcNow;
        var query = new GetTopSellersReportQuery(start, end, limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "INVALID_DATE_RANGE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}