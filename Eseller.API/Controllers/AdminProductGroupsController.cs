using Eseller.Application.Features.Products.ProductGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/product-groups")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminProductGroupsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminProductGroupsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get all product groups / batches.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? shopId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetProductGroupsQuery(shopId, search), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { items = result.Value, totalCount = result.Value.Count });
    }

    /// <summary>
    /// Get details of a product group with all product cards.
    /// </summary>
    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetById(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetProductGroupByIdQuery(batchId), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode == "GROUP_NOT_FOUND"
                ? NotFound(new { error = result.Error, errorCode = result.ErrorCode })
                : BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Remove a single product from the group & shop.
    /// </summary>
    [HttpDelete("{batchId:guid}/products/{productId:guid}")]
    public async Task<IActionResult> RemoveProduct(
        Guid batchId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new RemoveProductFromGroupCommand(batchId, productId), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ErrorCode == "PRODUCT_NOT_FOUND"
                ? NotFound(new { error = result.Error, errorCode = result.ErrorCode })
                : BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(new { message = "Product removed from group successfully." });
    }

    /// <summary>
    /// Replace a product in the group with another warehouse product.
    /// </summary>
    [HttpPost("{batchId:guid}/replace-product")]
    public async Task<IActionResult> ReplaceProduct(
        Guid batchId,
        [FromBody] ReplaceGroupProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new ReplaceProductInGroupCommand(batchId, request.OldProductId, request.NewWarehouseProductId),
            cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { product = result.Value, message = "Product replaced successfully." });
    }

    /// <summary>
    /// Add more products to the group (random or specific).
    /// </summary>
    [HttpPost("{batchId:guid}/add-products")]
    public async Task<IActionResult> AddProducts(
        Guid batchId,
        [FromBody] AddGroupProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new AddProductsToGroupCommand(batchId, request.AdditionalCount, request.SpecificWarehouseProductIds),
            cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { addedCount = result.Value, message = $"Successfully added {result.Value} products to the group." });
    }

    /// <summary>
    /// Delete the whole group and unlist all products.
    /// </summary>
    [HttpDelete("{batchId:guid}")]
    public async Task<IActionResult> DeleteGroup(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new DeleteProductGroupCommand(batchId), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Product group and all its listings deleted successfully." });
    }

    /// <summary>
    /// Approve all products in a group batch.
    /// </summary>
    [HttpPost("{batchId:guid}/approve")]
    public async Task<IActionResult> ApproveGroup(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ApproveProductGroupCommand(batchId), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { approvedCount = result.Value, message = $"Successfully approved {result.Value} products in the group batch." });
    }

    /// <summary>
    /// Approve a single product within a group batch.
    /// </summary>
    [HttpPost("{batchId:guid}/products/{productId:guid}/approve")]
    public async Task<IActionResult> ApproveProduct(
        Guid batchId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ApproveGroupProductCommand(batchId, productId), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Product in group approved successfully." });
    }
}

public record ReplaceGroupProductRequest(Guid OldProductId, Guid NewWarehouseProductId);
public record AddGroupProductsRequest(int AdditionalCount = 0, List<Guid>? SpecificWarehouseProductIds = null);
