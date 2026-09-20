using Eseller.Application.Features.Discounts.CreateDiscount;
using Eseller.Application.Features.Discounts.DeleteDiscount;
using Eseller.Application.Features.Discounts.UpdateDiscount;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/discounts-management")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class DiscountsManagementController : ControllerBase
{
    private readonly ISender _sender;

    public DiscountsManagementController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create a discount.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDiscountCommand(
            request.ProductId,
            request.ScopeType,
            request.ScopeId,
            request.Type,
            request.Value,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "PRODUCT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "SHOP_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "CATEGORY_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { discountId = result.Value, message = "Discount created successfully." });
    }

    /// <summary>
    /// Update a discount.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDiscountCommand(
            id,
            request.Type,
            request.Value,
            request.StartDate,
            request.EndDate,
            request.IsActive);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "DISCOUNT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Discount updated successfully." });
    }

    /// <summary>
    /// Delete a discount (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDiscountCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "DISCOUNT_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Discount deleted successfully." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record CreateDiscountRequest(
    Guid? ProductId,
    DiscountScopeType ScopeType,
    Guid? ScopeId,
    DiscountType Type,
    decimal Value,
    DateTime StartDate,
    DateTime EndDate);

public record UpdateDiscountRequest(
    DiscountType Type,
    decimal Value,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive);