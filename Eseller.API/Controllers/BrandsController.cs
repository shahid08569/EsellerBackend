using Eseller.Application.Features.Brands.GetBrandById;
using Eseller.Application.Features.Brands.GetBrandBySlug;
using Eseller.Application.Features.Brands.GetBrands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BrandsController : ControllerBase
{
    private readonly ISender _sender;

    public BrandsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all active brands (public).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetBrandsQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get brand by slug (public).
    /// </summary>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var query = new GetBrandBySlugQuery(slug);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get brand by ID (public).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetBrandByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
}