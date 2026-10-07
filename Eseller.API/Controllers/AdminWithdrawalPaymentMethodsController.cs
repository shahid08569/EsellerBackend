using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/withdrawal-payment-methods")]
[Authorize(Roles = "SuperAdmin")]
public class AdminWithdrawalPaymentMethodsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public AdminWithdrawalPaymentMethodsController(IApplicationDbContext context, IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var methods = await SellerWithdrawalPaymentMethodsStore.LoadAllAsync(
            _context, _dateTime, seedIfEmpty: true, cancellationToken);
        return Ok(new { items = methods });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] UpsertSellerWithdrawalPaymentMethodRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "Name is required.", errorCode = "INVALID_NAME" });

        var methods = (await SellerWithdrawalPaymentMethodsStore.LoadAllAsync(
            _context, _dateTime, seedIfEmpty: true, cancellationToken)).ToList();

        var created = new SellerWithdrawalPaymentMethodDto(
            Guid.NewGuid().ToString(),
            body.Name.Trim(),
            body.Details?.Trim() ?? string.Empty,
            body.IsActive);

        methods.Add(created);
        await SellerWithdrawalPaymentMethodsStore.SaveAllAsync(_context, _dateTime, methods, cancellationToken);

        return Ok(new { item = created, message = "Payment method created." });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpsertSellerWithdrawalPaymentMethodRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Id is required.", errorCode = "INVALID_ID" });

        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "Name is required.", errorCode = "INVALID_NAME" });

        var methods = (await SellerWithdrawalPaymentMethodsStore.LoadAllAsync(
            _context, _dateTime, seedIfEmpty: true, cancellationToken)).ToList();

        var index = methods.FindIndex(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return NotFound(new { error = "Payment method not found.", errorCode = "NOT_FOUND" });

        var updated = new SellerWithdrawalPaymentMethodDto(
            methods[index].Id,
            body.Name.Trim(),
            body.Details?.Trim() ?? string.Empty,
            body.IsActive);

        methods[index] = updated;
        await SellerWithdrawalPaymentMethodsStore.SaveAllAsync(_context, _dateTime, methods, cancellationToken);

        return Ok(new { item = updated, message = "Payment method updated." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Id is required.", errorCode = "INVALID_ID" });

        var methods = (await SellerWithdrawalPaymentMethodsStore.LoadAllAsync(
            _context, _dateTime, seedIfEmpty: true, cancellationToken)).ToList();

        var removed = methods.RemoveAll(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return NotFound(new { error = "Payment method not found.", errorCode = "NOT_FOUND" });

        await SellerWithdrawalPaymentMethodsStore.SaveAllAsync(_context, _dateTime, methods, cancellationToken);

        return Ok(new { message = "Payment method deleted." });
    }
}
