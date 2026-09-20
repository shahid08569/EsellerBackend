using Eseller.Application.Features.Coupons.CreateCoupon;
using Eseller.Application.Features.Coupons.DeleteCoupon;
using Eseller.Application.Features.Coupons.GetCouponUsages;
using Eseller.Application.Features.Coupons.UpdateCoupon;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/coupons-management")]
[Authorize(Roles = "Shopkeeper,SuperAdmin,Partner")]
public class CouponsManagementController : ControllerBase
{
    private readonly ISender _sender;

    public CouponsManagementController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Create a new coupon.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCouponRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCouponCommand(
            request.Code,
            request.Type,
            request.Value,
            request.MinOrderAmount,
            request.MaxDiscount,
            request.ExpiryDate,
            request.UsageLimit,
            request.ScopeType,
            request.ProductIds,
            request.CategoryIds,
            request.ShopIds,
            request.CustomerIds);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "CODE_TAKEN" => Conflict(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { couponId = result.Value, message = "Coupon created successfully." });
    }

    /// <summary>
    /// Update a coupon.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCouponRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCouponCommand(
            id, request.Value, request.MinOrderAmount,
            request.MaxDiscount, request.ExpiryDate,
            request.UsageLimit, request.IsActive);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "COUPON_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Coupon updated successfully." });
    }

    /// <summary>
    /// Delete a coupon (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteCouponCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "COUPON_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Coupon deleted successfully." });
    }

    /// <summary>
    /// Get coupon usage history.
    /// </summary>
    [HttpGet("{id:guid}/usages")]
    public async Task<IActionResult> GetUsages(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCouponUsagesQuery(id, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "COUPON_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record CreateCouponRequest(
    string Code,
    DiscountType Type,
    decimal Value,
    decimal? MinOrderAmount,
    decimal? MaxDiscount,
    DateTime? ExpiryDate,
    int? UsageLimit,
    CouponScopeType ScopeType,
    List<Guid>? ProductIds,
    List<Guid>? CategoryIds,
    List<Guid>? ShopIds,
    List<Guid>? CustomerIds);

public record UpdateCouponRequest(
    decimal Value,
    decimal? MinOrderAmount,
    decimal? MaxDiscount,
    DateTime? ExpiryDate,
    int? UsageLimit,
    bool IsActive);