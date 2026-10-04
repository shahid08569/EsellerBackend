using Eseller.Application.Features.ShopCategories.GetShopCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/shop-categories")]
[AllowAnonymous]
public class ShopCategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public ShopCategoriesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get active shop categories/tiers (public for filtering and badges).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetShopCategoriesQuery(OnlyActive: true);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }
}
