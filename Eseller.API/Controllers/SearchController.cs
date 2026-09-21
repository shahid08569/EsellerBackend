using Eseller.Application.Features.Search.GetAutocomplete;
using Eseller.Application.Features.Search.GetSuggestions;
using Eseller.Application.Features.Search.GetTrendingSearches;
using Eseller.Application.Features.Search.SearchProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class SearchController : ControllerBase
{
    private readonly ISender _sender;

    public SearchController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Full product search with filters, sorting, pagination.
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] string q,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? shopId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minRating,
        [FromQuery] bool? isFeatured,
        [FromQuery] string sortBy = "relevance",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new SearchProductsQuery(
            q, categoryId, brandId, shopId,
            minPrice, maxPrice, minRating, isFeatured,
            sortBy, pageNumber, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Autocomplete suggestions (top 10 mixed).
    /// </summary>
    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAutocompleteQuery(q, limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Trending searches (top viewed products).
    /// </summary>
    [HttpGet("trending")]
    public async Task<IActionResult> Trending(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTrendingSearchesQuery(limit);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Detailed suggestions (products + categories + brands grouped).
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> Suggestions(
        [FromQuery] string q,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSuggestionsQuery(q);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
}