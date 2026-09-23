using Eseller.Application.Features.Banners.CreateBanner;
using Eseller.Application.Features.Banners.DeleteBanner;
using Eseller.Application.Features.Banners.GetBannerById;
using Eseller.Application.Features.Banners.GetHomepageBanners;
using Eseller.Application.Features.Banners.UpdateBanner;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/banners")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminBannersController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBannersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetBanners(
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 200) pageSize = 200;
        if (pageSize < 1) pageSize = 50;

        var query = new GetHomepageBannersQuery(isActive, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetBannerByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBannerCommand(
            request.ImageUrl,
            request.LinkUrl,
            request.SortOrder,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            bannerId = result.Value,
            message = "Banner created successfully."
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBannerCommand(
            id,
            request.ImageUrl,
            request.LinkUrl,
            request.SortOrder,
            request.IsActive,
            request.StartDate,
            request.EndDate);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Banner updated successfully." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteBannerCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Banner deleted successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "BANNER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "BANNER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record CreateBannerRequest(
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    DateTime? StartDate,
    DateTime? EndDate);

public record UpdateBannerRequest(
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTime? StartDate,
    DateTime? EndDate);