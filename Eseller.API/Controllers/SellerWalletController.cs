using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/SellerWallet")]
[Authorize(Roles = "Shopkeeper")]
public class SellerWalletController : ControllerBase
{
    public const string MinimumWithdrawalSettingKey = "DefaultWithdrawalThreshold";
    private const decimal FallbackMinimumWithdrawal = 1000m;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _dateTime;

    public SellerWalletController(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTime dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    /// <summary>Wallet balance + earning totals (20% merchant share).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var accountId = _currentUser.AccountId;
        if (accountId is null) return Unauthorized();

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == accountId.Value, cancellationToken);
        if (shopkeeper is null)
            return NotFound(new { error = "Seller profile not found.", errorCode = "SHOPKEEPER_NOT_FOUND" });

        var shopIds = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var sellerOrderIds = await _context.SellerOrders
            .Where(so => shopIds.Contains(so.ShopId))
            .Select(so => so.Id)
            .ToListAsync(cancellationToken);

        var earnings = sellerOrderIds.Count == 0
            ? []
            : await _context.SellerEarnings
                .Where(se => sellerOrderIds.Contains(se.SellerOrderId))
                .ToListAsync(cancellationToken);

        decimal AmountOf(SellerEarning e) => e.NetAmount > 0 ? e.NetAmount : e.CommissionAmount;

        var pendingFromOrders = earnings.Where(e => e.Status == SellerEarningStatus.Pending).Sum(AmountOf);
        var availableFromOrders = earnings.Where(e => e.Status == SellerEarningStatus.Available).Sum(AmountOf);
        var withdrawn = earnings.Where(e => e.Status == SellerEarningStatus.Withdrawn).Sum(AmountOf);

        var wallet = await EnsureWalletAsync(
            accountId.Value,
            pendingFromOrders,
            availableFromOrders,
            cancellationToken);

        var pendingWithdrawals = await _context.Withdrawals
            .Where(w => w.AccountId == accountId.Value && w.Status == WithdrawalStatus.Pending)
            .SumAsync(w => w.Amount, cancellationToken);

        var minimumWithdrawal = await ResolveMinimumWithdrawalAsync(cancellationToken);

        return Ok(new
        {
            walletBalance = wallet.Balance,
            pendingEarnings = wallet.PendingEarnings,
            availableEarnings = wallet.AvailableEarnings,
            withdrawnEarnings = withdrawn,
            pendingWithdrawals,
            availableToWithdraw = wallet.Balance,
            minimumWithdrawal,
            merchantSharePercent = 20
        });
    }

    /// <summary>Saved bank/card details for payouts (masked; CVC is never returned).</summary>
    [HttpGet("bank-details")]
    public async Task<IActionResult> GetBankDetails(CancellationToken cancellationToken)
    {
        var accountId = _currentUser.AccountId;
        if (accountId is null) return Unauthorized();

        var details = await SellerBankDetailsStore.LoadForSellerAsync(
            _context, accountId.Value, cancellationToken);

        if (details is null)
            return Ok(new { });

        return Ok(details);
    }

    [HttpPut("bank-details")]
    public async Task<IActionResult> PutBankDetails(
        [FromBody] UpsertSellerBankDetailsRequest body,
        CancellationToken cancellationToken)
    {
        var accountId = _currentUser.AccountId;
        if (accountId is null) return Unauthorized();

        var validationError = SellerBankDetailsStore.ValidateUpsert(body, _dateTime.UtcNow);
        if (validationError is not null)
            return BadRequest(new { error = validationError, errorCode = "INVALID_BANK_DETAILS" });

        var saved = await SellerBankDetailsStore.SaveAsync(
            _context, _dateTime, accountId.Value, body, cancellationToken);

        return Ok(saved);
    }

    /// <summary>Platform-managed payout methods for seller withdrawal UI.</summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods(CancellationToken cancellationToken)
    {
        var methods = await SellerWithdrawalPaymentMethodsStore.LoadAllAsync(
            _context, _dateTime, seedIfEmpty: true, cancellationToken);

        var active = methods.Where(m => m.IsActive).ToList();
        return Ok(new { items = active });
    }

    [HttpGet("withdrawals")]
    public async Task<IActionResult> GetWithdrawals(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var accountId = _currentUser.AccountId;
        if (accountId is null) return Unauthorized();

        if (pageSize > 100) pageSize = 100;
        if (pageNumber < 1) pageNumber = 1;

        var query = _context.Withdrawals
            .Where(w => w.AccountId == accountId.Value)
            .OrderByDescending(w => w.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new
            {
                w.Id,
                w.Amount,
                w.PayoutMethod,
                status = w.Status.ToString(),
                w.RequestedAt,
                w.ProcessedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items, totalCount, pageNumber, pageSize });
    }

    [HttpPost("withdrawal-request")]
    public async Task<IActionResult> RequestWithdrawal(
        [FromBody] SellerWithdrawalRequest body,
        CancellationToken cancellationToken)
    {
        var accountId = _currentUser.AccountId;
        if (accountId is null) return Unauthorized();

        var minimumWithdrawal = await ResolveMinimumWithdrawalAsync(cancellationToken);
        if (body.Amount < minimumWithdrawal)
            return BadRequest(new
            {
                error = $"Minimum withdrawal amount is $ {minimumWithdrawal:N0}.",
                errorCode = "BELOW_MINIMUM"
            });

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == accountId.Value, cancellationToken);
        if (shopkeeper is null)
            return NotFound(new { error = "Seller profile not found.", errorCode = "SHOPKEEPER_NOT_FOUND" });

        if (shopkeeper.Status != ShopkeeperApprovalStatus.Approved)
            return BadRequest(new
            {
                error = "Store must be approved before requesting a withdrawal.",
                errorCode = "STORE_NOT_APPROVED"
            });

        var wallet = await EnsureWalletAsync(accountId.Value, 0, 0, cancellationToken);
        if (wallet.Balance < body.Amount)
            return BadRequest(new { error = "Insufficient balance.", errorCode = "INSUFFICIENT_BALANCE" });

        var hasPending = await _context.Withdrawals.AnyAsync(
            w => w.AccountId == accountId.Value && w.Status == WithdrawalStatus.Pending,
            cancellationToken);

        if (hasPending)
            return BadRequest(new
            {
                error = "You already have a pending withdrawal request.",
                errorCode = "PENDING_WITHDRAWAL_EXISTS"
            });

        var now = _dateTime.UtcNow;
        var method = string.IsNullOrWhiteSpace(body.PayoutMethod)
            ? "Bank Transfer"
            : body.PayoutMethod.Trim();
        if (!string.IsNullOrWhiteSpace(body.AccountDetails))
            method = $"{method} | {body.AccountDetails.Trim()}";

        var withdrawal = new Withdrawal
        {
            AccountId = accountId.Value,
            Amount = body.Amount,
            PayoutMethod = method,
            Status = WithdrawalStatus.Pending,
            RequestedAt = now
        };
        withdrawal.SetCreatedAt(now);
        withdrawal.SetUpdatedAt(now);
        _context.Withdrawals.Add(withdrawal);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = withdrawal.Id,
            message = "Withdrawal request submitted to Super Admin."
        });
    }

    private async Task<decimal> ResolveMinimumWithdrawalAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.Key == MinimumWithdrawalSettingKey
                && s.IsActive, cancellationToken);

        if (setting is not null
            && decimal.TryParse(setting.Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
            return parsed;

        return FallbackMinimumWithdrawal;
    }

    private async Task<Wallet> EnsureWalletAsync(
        Guid accountId,
        decimal seedPending,
        decimal seedAvailable,
        CancellationToken cancellationToken)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);
        if (wallet is not null) return wallet;

        var now = _dateTime.UtcNow;
        wallet = new Wallet
        {
            AccountId = accountId,
            Balance = 0,
            PendingEarnings = seedPending,
            AvailableEarnings = seedAvailable
        };
        wallet.SetCreatedAt(now);
        wallet.SetUpdatedAt(now);
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync(cancellationToken);
        return wallet;
    }
}

public record SellerWithdrawalRequest(
    decimal Amount,
    string? PayoutMethod,
    string? AccountDetails);
