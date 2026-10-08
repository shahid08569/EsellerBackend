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
[Route("api/v1/admin/seller-wallets")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminSellerWalletsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public AdminSellerWalletsController(IApplicationDbContext context, IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    /// <summary>All shopkeepers with shop name, wallet balance, and earning totals.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            var shopkeepers = await _context.Shopkeepers
                .AsNoTracking()
                .Select(sk => new { sk.Id, sk.AccountId, sk.Name, sk.Status })
                .ToListAsync(cancellationToken);

            if (shopkeepers.Count == 0)
                return Ok(new { items = Array.Empty<object>() });

            var keeperIds = shopkeepers.Select(sk => sk.Id).ToList();

            var shops = await _context.Shops
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.DeletedAt == null && keeperIds.Contains(s.ShopkeeperId))
                .Select(s => new { s.Id, s.ShopkeeperId, s.Name, s.CreatedAt })
                .ToListAsync(cancellationToken);

            var shopNameByKeeper = shops
                .GroupBy(s => s.ShopkeeperId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(s => s.CreatedAt).First().Name);

            // Show keepers that have a shop, or are Approved/Pending (even pre-shop).
            var visibleKeepers = shopkeepers
                .Where(sk =>
                    shopNameByKeeper.ContainsKey(sk.Id)
                    || sk.Status == ShopkeeperApprovalStatus.Approved
                    || sk.Status == ShopkeeperApprovalStatus.Pending)
                .ToList();

            var accountIds = visibleKeepers.Select(sk => sk.AccountId).Distinct().ToList();
            var wallets = await LoadWalletTotalsAsync(accountIds, cancellationToken);

            // Fallback for accounts that still only have SellerEarnings ledger rows
            var shopIdToKeeper = shops
                .GroupBy(s => s.Id)
                .ToDictionary(g => g.Key, g => g.First().ShopkeeperId);
            var shopIds = shopIdToKeeper.Keys.ToList();
            var sellerOrderRows = shopIds.Count == 0
                ? []
                : await _context.SellerOrders
                    .AsNoTracking()
                    .Where(so => shopIds.Contains(so.ShopId))
                    .Select(so => new { so.Id, so.ShopId })
                    .ToListAsync(cancellationToken);
            var orderIdToKeeper = sellerOrderRows
                .Where(r => shopIdToKeeper.ContainsKey(r.ShopId))
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => shopIdToKeeper[g.First().ShopId]);
            var sellerOrderIds = orderIdToKeeper.Keys.ToList();
            var earnings = sellerOrderIds.Count == 0
                ? []
                : await _context.SellerEarnings
                    .AsNoTracking()
                    .Where(se => sellerOrderIds.Contains(se.SellerOrderId))
                    .Select(se => new { se.SellerOrderId, se.Status, se.NetAmount, se.CommissionAmount })
                    .ToListAsync(cancellationToken);

            var pendingByKeeper = new Dictionary<Guid, decimal>();
            var availableByKeeper = new Dictionary<Guid, decimal>();
            foreach (var e in earnings)
            {
                if (!orderIdToKeeper.TryGetValue(e.SellerOrderId, out var keeperId))
                    continue;
                var amount = e.NetAmount > 0 ? e.NetAmount : e.CommissionAmount;
                if (e.Status == SellerEarningStatus.Pending)
                    pendingByKeeper[keeperId] = pendingByKeeper.GetValueOrDefault(keeperId) + amount;
                else if (e.Status == SellerEarningStatus.Available)
                    availableByKeeper[keeperId] = availableByKeeper.GetValueOrDefault(keeperId) + amount;
            }

            var items = visibleKeepers
                .OrderBy(sk => sk.Name)
                .Select(sk =>
                {
                    wallets.TryGetValue(sk.AccountId, out var wallet);
                    var pendingFromOrders = pendingByKeeper.GetValueOrDefault(sk.Id, 0m);
                    var availableFromOrders = availableByKeeper.GetValueOrDefault(sk.Id, 0m);
                    // Wallet columns are source of truth after Management adjust / migration backfill.
                    // No wallet row yet → fall back to order ledger sums.
                    var pending = wallet?.PendingEarnings ?? pendingFromOrders;
                    var available = wallet?.AvailableEarnings ?? availableFromOrders;

                    return new
                    {
                        accountId = sk.AccountId,
                        shopkeeperName = sk.Name,
                        shopName = shopNameByKeeper.GetValueOrDefault(sk.Id),
                        walletBalance = wallet?.Balance ?? 0m,
                        pendingEarnings = pending,
                        availableEarnings = available
                    };
                })
                .ToList();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Failed to load seller wallets.",
                errorCode = "SELLER_WALLETS_LIST_FAILED",
                detail = ex.Message
            });
        }
    }

    private async Task<Dictionary<Guid, WalletTotals>> LoadWalletTotalsAsync(
        List<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
            return new Dictionary<Guid, WalletTotals>();

        try
        {
            var rows = await _context.Wallets
                .AsNoTracking()
                .Where(w => accountIds.Contains(w.AccountId))
                .Select(w => new
                {
                    w.AccountId,
                    w.Balance,
                    w.PendingEarnings,
                    w.AvailableEarnings
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var w = g.First();
                        return new WalletTotals(w.Balance, w.PendingEarnings, w.AvailableEarnings);
                    });
        }
        catch
        {
            // Deploy race: PendingEarnings / AvailableEarnings columns may be missing briefly.
            // Fall back to Balance only so the Seller Earnings table still renders.
            var rows = await _context.Wallets
                .AsNoTracking()
                .Where(w => accountIds.Contains(w.AccountId))
                .Select(w => new { w.AccountId, w.Balance })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => new WalletTotals(g.First().Balance, 0m, 0m));
        }
    }

    private sealed record WalletTotals(decimal Balance, decimal PendingEarnings, decimal AvailableEarnings);

    /// <summary>
    /// Seller payout bank/card details for manual processing.
    /// Includes full card number and CVC (Super Admin only).
    /// </summary>
    [HttpGet("{accountId:guid}/bank-details")]
    public async Task<IActionResult> GetBankDetails(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var shopkeeper = await _context.Shopkeepers
            .AsNoTracking()
            .FirstOrDefaultAsync(sk => sk.AccountId == accountId, cancellationToken);

        if (shopkeeper is null)
            return NotFound(new { error = "Shopkeeper not found for this account.", errorCode = "SHOPKEEPER_NOT_FOUND" });

        var details = await SellerBankDetailsStore.LoadForAdminAsync(
            _context, accountId, cancellationToken);

        if (details is null)
            return Ok(new { });

        return Ok(details);
    }

    /// <summary>
    /// Management sets seller Pending / Available totals on the wallet (always succeeds).
    /// Seller panel reads the same wallet fields. Wallet Balance is set via PUT .../balance.
    /// </summary>
    [HttpPut("{accountId:guid}/earnings")]
    public async Task<IActionResult> SetEarnings(
        Guid accountId,
        [FromBody] SetSellerEarningsRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required.", errorCode = "BODY_REQUIRED" });

        if (body.PendingEarnings < 0 || body.AvailableEarnings < 0)
            return BadRequest(new { error = "Earnings cannot be negative.", errorCode = "INVALID_EARNINGS" });

        var shopkeeper = await _context.Shopkeepers
            .AsNoTracking()
            .FirstOrDefaultAsync(sk => sk.AccountId == accountId, cancellationToken);

        if (shopkeeper is null)
            return NotFound(new { error = "Shopkeeper not found for this account.", errorCode = "SHOPKEEPER_NOT_FOUND" });

        var now = _dateTime.UtcNow;
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);

        if (wallet is null)
        {
            wallet = new Wallet { AccountId = accountId, Balance = 0 };
            wallet.SetCreatedAt(now);
            wallet.SetUpdatedAt(now);
            _context.Wallets.Add(wallet);
        }

        var previousPending = wallet.PendingEarnings;
        var previousAvailable = wallet.AvailableEarnings;

        wallet.PendingEarnings = body.PendingEarnings;
        wallet.AvailableEarnings = body.AvailableEarnings;
        wallet.SetUpdatedAt(now);

        // Persist wallet first so Adjust always succeeds even if ledger sync fails.
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await SyncSellerEarningsLedgerAsync(
                shopkeeper.Id,
                body.PendingEarnings,
                body.AvailableEarnings,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Wallet totals are the source of truth for Management adjust
        }

        return Ok(new
        {
            accountId,
            pendingEarnings = wallet.PendingEarnings,
            availableEarnings = wallet.AvailableEarnings,
            previousPending,
            previousAvailable,
            message = "Seller earnings updated."
        });
    }

    private async Task SyncSellerEarningsLedgerAsync(
        Guid shopkeeperId,
        decimal pendingTarget,
        decimal availableTarget,
        CancellationToken cancellationToken)
    {
        var shopIds = await _context.Shops
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.DeletedAt == null && s.ShopkeeperId == shopkeeperId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (shopIds.Count == 0) return;

        var sellerOrderIds = await _context.SellerOrders
            .Where(so => shopIds.Contains(so.ShopId))
            .OrderByDescending(so => so.Id)
            .Select(so => so.Id)
            .ToListAsync(cancellationToken);

        if (sellerOrderIds.Count == 0) return;

        var fallbackSellerOrderId = sellerOrderIds[0];
        var earnings = await _context.SellerEarnings
            .Where(se => sellerOrderIds.Contains(se.SellerOrderId)
                      && (se.Status == SellerEarningStatus.Pending
                          || se.Status == SellerEarningStatus.Available))
            .ToListAsync(cancellationToken);

        AdjustEarningsGroup(
            earnings.Where(e => e.Status == SellerEarningStatus.Pending).ToList(),
            pendingTarget,
            SellerEarningStatus.Pending,
            fallbackSellerOrderId);

        AdjustEarningsGroup(
            earnings.Where(e => e.Status == SellerEarningStatus.Available).ToList(),
            availableTarget,
            SellerEarningStatus.Available,
            fallbackSellerOrderId);
    }

    private void AdjustEarningsGroup(
        List<SellerEarning> rows,
        decimal target,
        SellerEarningStatus status,
        Guid fallbackSellerOrderId)
    {
        static decimal AmountOf(SellerEarning e) =>
            e.NetAmount > 0 ? e.NetAmount : e.CommissionAmount;

        if (rows.Count == 0)
        {
            if (target == 0 || fallbackSellerOrderId == Guid.Empty) return;
            _context.SellerEarnings.Add(new SellerEarning
            {
                SellerOrderId = fallbackSellerOrderId,
                GrossAmount = target * 5m,
                CommissionAmount = target,
                NetAmount = target,
                Status = status
            });
            return;
        }

        var current = rows.Sum(AmountOf);
        if (current <= 0)
        {
            rows[0].NetAmount = target;
            rows[0].CommissionAmount = target;
            if (rows[0].GrossAmount <= 0)
                rows[0].GrossAmount = target * 5m;
            for (var i = 1; i < rows.Count; i++)
            {
                rows[i].NetAmount = 0;
                rows[i].CommissionAmount = 0;
            }
            return;
        }

        var allocated = 0m;
        for (var i = 0; i < rows.Count; i++)
        {
            var share = i == rows.Count - 1
                ? target - allocated
                : Math.Round(AmountOf(rows[i]) / current * target, 2);
            if (share < 0) share = 0;
            allocated += share;
            rows[i].NetAmount = share;
            rows[i].CommissionAmount = share;
        }
    }

    /// <summary>Set wallet balance for a seller account (creates wallet if missing).</summary>
    [HttpPut("{accountId:guid}/balance")]
    public async Task<IActionResult> SetBalance(
        Guid accountId,
        [FromBody] SetSellerWalletBalanceRequest body,
        CancellationToken cancellationToken)
    {
        if (body.Balance < 0)
            return BadRequest(new { error = "Balance cannot be negative.", errorCode = "INVALID_BALANCE" });

        var shopkeeper = await _context.Shopkeepers
            .AsNoTracking()
            .FirstOrDefaultAsync(sk => sk.AccountId == accountId, cancellationToken);

        if (shopkeeper is null)
            return NotFound(new { error = "Shopkeeper not found for this account.", errorCode = "SHOPKEEPER_NOT_FOUND" });

        var now = _dateTime.UtcNow;
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);

        if (wallet is null)
        {
            wallet = new Wallet { AccountId = accountId, Balance = 0 };
            wallet.SetCreatedAt(now);
            wallet.SetUpdatedAt(now);
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var previousBalance = wallet.Balance;
        var delta = body.Balance - previousBalance;

        if (delta != 0)
        {
            wallet.Balance = body.Balance;
            wallet.SetUpdatedAt(now);

            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Direction = delta > 0 ? WalletDirection.Credit : WalletDirection.Debit,
                TransactionType = WalletTransactionType.ManualAdjustment,
                Amount = Math.Abs(delta),
                ReferenceType = "AdminAdjustment",
                ReferenceId = null,
                Description = $"Super Admin balance adjustment ({previousBalance:N2} → {body.Balance:N2})",
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            accountId,
            walletBalance = wallet.Balance,
            previousBalance,
            message = delta == 0 ? "Balance unchanged." : "Wallet balance updated."
        });
    }
}

public record SetSellerWalletBalanceRequest(decimal Balance);

public record SetSellerEarningsRequest(decimal PendingEarnings, decimal AvailableEarnings);
