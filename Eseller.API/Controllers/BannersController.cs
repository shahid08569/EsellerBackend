using Eseller.Application.Features.Banners.GetActiveBanners;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/banners")]
[AllowAnonymous]
public class BannersController : ControllerBase
{
    private readonly ISender _sender;

    public BannersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get currently active homepage banners (public).
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var query = new GetActiveBannersQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
}