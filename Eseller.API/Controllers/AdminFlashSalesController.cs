using Eseller.Application.Features.FlashSales.CreateFlashSale;
using Eseller.Application.Features.FlashSales.DeleteFlashSale;
using Eseller.Application.Features.FlashSales.GetFlashSaleById;
using Eseller.Application.Features.FlashSales.GetFlashSales;
using Eseller.Application.Features.FlashSales.UpdateFlashSale;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/flash-sales")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminFlashSalesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminFlashSalesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetFlashSales(
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;

        var query = new GetFlashSalesQuery(isActive, pageNumber, pageSize);
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
        var query = new GetFlashSaleByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFlashSaleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateFlashSaleCommand(
            request.Name,
            request.StartDate,
            request.EndDate,
            request.Products.Select(p => new FlashSaleProductInput(
                p.ProductId, p.DiscountType, p.DiscountValue)).ToList());

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new
        {
            flashSaleId = result.Value,
            message = "Flash sale created successfully."
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateFlashSaleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateFlashSaleCommand(
            id,
            request.Name,
            request.StartDate,
            request.EndDate,
            request.IsActive,
            request.Products.Select(p => new FlashSaleProductInput(
                p.ProductId, p.DiscountType, p.DiscountValue)).ToList());

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Flash sale updated successfully." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteFlashSaleCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return HandleError(result);

        return Ok(new { message = "Flash sale deleted successfully." });
    }

    private IActionResult HandleError<T>(Eseller.Application.Common.Result<T> result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "FLASHSALE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "PRODUCTS_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }

    private IActionResult HandleError(Eseller.Application.Common.Result result)
    {
        return result.ErrorCode switch
        {
            "UNAUTHENTICATED" => Unauthorized(new { error = result.Error, errorCode = result.ErrorCode }),
            "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
            "FLASHSALE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
            "PRODUCTS_NOT_AVAILABLE" => BadRequest(new { error = result.Error, errorCode = result.ErrorCode }),
            _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
        };
    }
}

public record CreateFlashSaleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    List<FlashSaleProductRequest> Products);

public record UpdateFlashSaleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    List<FlashSaleProductRequest> Products);

public record FlashSaleProductRequest(
    Guid ProductId,
    int DiscountType,
    decimal DiscountValue);