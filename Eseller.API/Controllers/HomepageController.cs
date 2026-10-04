using Eseller.Application.Features.Homepage.GetBestSellingProducts;
using Eseller.Application.Features.Homepage.GetFlashSaleProducts;
using Eseller.Application.Features.Homepage.GetHomepageCategories;
using Eseller.Application.Features.Homepage.GetHomepageSections;
using Eseller.Application.Features.Homepage.GetHotSellingProducts;
using Eseller.Application.Features.Homepage.GetLowStockProducts;
using Eseller.Application.Features.Homepage.GetNewArrivals;
using Eseller.Application.Features.Homepage.GetOutOfStockProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/homepage")]
[AllowAnonymous]
public class HomepageController : ControllerBase
{
    private readonly ISender _sender;

    public HomepageController(ISender sender)
    {
        _sender = sender;
    }

    // ═══════════════════════════════════════════════════════════
    // ALL SECTIONS (Ek call mein sab)
    // ═══════════════════════════════════════════════════════════
    [HttpGet("sections")]
    public async Task<IActionResult> GetSections(
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var query = new GetHomepageSectionsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    // ═══════════════════════════════════════════════════════════
    // INDIVIDUAL SECTIONS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("best-selling")]
    public async Task<IActionResult> GetBestSelling(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetBestSellingProductsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet("hot-selling")]
    public async Task<IActionResult> GetHotSelling(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetHotSellingProductsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet("new-arrivals")]
    public async Task<IActionResult> GetNewArrivals(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetNewArrivalsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet("flash-sale")]
    public async Task<IActionResult> GetFlashSale(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFlashSaleProductsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetLowStockProductsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    [HttpGet("out-of-stock")]
    public async Task<IActionResult> GetOutOfStock(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOutOfStockProductsQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
    [FromQuery] int limit = 11,
    [FromQuery] int productsPerCategory = 8,
    CancellationToken cancellationToken = default)
    {
        var query = new GetHomepageCategoriesQuery(limit, productsPerCategory);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
}