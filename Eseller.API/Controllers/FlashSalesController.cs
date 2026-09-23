using Eseller.Application.Features.FlashSales.GetActiveFlashSales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/flash-sales")]
[AllowAnonymous]
public class FlashSalesController : ControllerBase
{
    private readonly ISender _sender;

    public FlashSalesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get currently active flash sales (public).
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 50) pageSize = 50;
        if (pageSize < 1) pageSize = 20;

        var query = new GetActiveFlashSalesQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
}