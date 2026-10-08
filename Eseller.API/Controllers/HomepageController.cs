using Eseller.Application.Features.Homepage.GetBestSellingProducts;
using Eseller.Application.Features.Homepage.GetFlashSaleProducts;
using Eseller.Application.Features.Homepage.GetHomepageCategories;
using Eseller.Application.Features.Homepage.GetHomepageSections;
using Eseller.Application.Features.Homepage.GetHotSellingProducts;
using Eseller.Application.Features.Homepage.GetLowStockProducts;
using Eseller.Application.Features.Homepage.GetNewArrivals;
using Eseller.Application.Features.Homepage.GetOutOfStockProducts;
using Eseller.Application.Features.Homepage.GetPlatformBranding;
using Eseller.Application.Features.PaymentShowcase.GetActivePaymentLogos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/homepage")]
[AllowAnonymous]
public class HomepageController : ControllerBase
{
    private static readonly TimeSpan SectionsCacheTtl = TimeSpan.FromSeconds(60);

    private readonly ISender _sender;
    private readonly IMemoryCache _cache;

    public HomepageController(ISender sender, IMemoryCache cache)
    {
        _sender = sender;
        _cache = cache;
    }

    // ═══════════════════════════════════════════════════════════
    // ALL SECTIONS (Ek call mein sab) — cached for MonsterASP speed
    // ═══════════════════════════════════════════════════════════
    [HttpGet("sections")]
    public async Task<IActionResult> GetSections(
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = limit > 20 ? 20 : (limit < 1 ? 8 : limit);
        var cacheKey = $"homepage:sections:v1:{safeLimit}";

        if (_cache.TryGetValue(cacheKey, out HomepageSectionsDto? cached) && cached is not null)
            return Ok(cached);

        var query = new GetHomepageSectionsQuery(safeLimit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        _cache.Set(cacheKey, result.Value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SectionsCacheTtl
        });

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

    /// <summary>
    /// Active payment-method logos for the homepage showcase marquee.
    /// </summary>
    [HttpGet("payment-logos")]
    public async Task<IActionResult> GetPaymentLogos(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "homepage:payment-logos:v1";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            return Ok(cached);

        var result = await _sender.Send(new GetActivePaymentLogosQuery(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        _cache.Set(cacheKey, result.Value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        });

        return Ok(result.Value);
    }

    /// <summary>
    /// Storefront nav / footer branding (SuperAdmin-editable via Settings keys).
    /// </summary>
    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "homepage:branding:v2";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            return Ok(cached);

        var result = await _sender.Send(new GetPlatformBrandingQuery(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        _cache.Set(cacheKey, result.Value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        });

        return Ok(result.Value);
    }
}