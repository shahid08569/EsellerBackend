using Eseller.Application.Features.Dashboard.CreateAddress;
using Eseller.Application.Features.Dashboard.DeleteAddress;
using Eseller.Application.Features.Dashboard.GetAddresses;
using Eseller.Application.Features.Dashboard.GetDashboardSummary;
using Eseller.Application.Features.Dashboard.GetNotifications;
using Eseller.Application.Features.Dashboard.GetProfile;
using Eseller.Application.Features.Dashboard.MarkAllNotificationsRead;
using Eseller.Application.Features.Dashboard.MarkNotificationRead;
using Eseller.Application.Features.Dashboard.UpdateAddress;
using Eseller.Application.Features.Dashboard.UpdateProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    // ═══════════════════════════════════════════════════════════
    // DASHBOARD SUMMARY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get dashboard summary stats.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var query = new GetDashboardSummaryQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    // ═══════════════════════════════════════════════════════════
    // PROFILE (Customer + Seller)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get current user / seller profile.
    /// </summary>
    [HttpGet("profile")]
    [Authorize(Roles = "User,Shopkeeper")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var query = new GetProfileQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Update current user / seller profile.
    /// </summary>
    [HttpPut("profile")]
    [Authorize(Roles = "User,Shopkeeper")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProfileCommand(request.Name, request.Phone);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "Profile updated successfully." });
    }

    // ═══════════════════════════════════════════════════════════
    // ADDRESSES
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get all addresses.
    /// </summary>
    [HttpGet("addresses")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var query = new GetAddressesQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Create a new address.
    /// </summary>
    [HttpPost("addresses")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CreateAddress(
        [FromBody] CreateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAddressCommand(
            request.Label, request.FullName, request.Phone,
            request.AddressLine1, request.AddressLine2,
            request.City, request.State, request.Country,
            request.PostalCode, request.IsDefault);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { addressId = result.Value, message = "Address created." });
    }

    /// <summary>
    /// Update an address.
    /// </summary>
    [HttpPut("addresses/{id:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> UpdateAddress(
        Guid id,
        [FromBody] UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAddressCommand(
            id, request.Label, request.FullName, request.Phone,
            request.AddressLine1, request.AddressLine2,
            request.City, request.State, request.Country,
            request.PostalCode, request.IsDefault);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ADDRESS_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Address updated." });
    }

    /// <summary>
    /// Delete an address (soft delete).
    /// </summary>
    [HttpDelete("addresses/{id:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> DeleteAddress(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteAddressCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ADDRESS_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Address deleted." });
    }

    // ═══════════════════════════════════════════════════════════
    // NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get notifications (paginated).
    /// </summary>
    [HttpGet("notifications")]
    [Authorize(Roles = "User,Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetNotificationsQuery(unreadOnly, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>
    /// Mark notification as read.
    /// </summary>
    [HttpPut("notifications/{id:guid}/read")]
    [Authorize(Roles = "User,Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> MarkNotificationRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new MarkNotificationReadCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "NOTIFICATION_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Notification marked as read." });
    }

    /// <summary>
    /// Mark all notifications as read.
    /// </summary>
    [HttpPut("notifications/read-all")]
    [Authorize(Roles = "User,Shopkeeper,SuperAdmin,Partner")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
    {
        var command = new MarkAllNotificationsReadCommand();
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { message = "All notifications marked as read." });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record UpdateProfileRequest(string Name, string? Phone);

public record CreateAddressRequest(
    string Label,
    string FullName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? State,
    string Country,
    string? PostalCode,
    bool IsDefault);

public record UpdateAddressRequest(
    string Label,
    string FullName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? State,
    string Country,
    string? PostalCode,
    bool IsDefault);