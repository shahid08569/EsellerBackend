using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/seller-wallets")]
[Authorize(Roles = "SuperAdmin")]
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
        var shopkeepers = await _context.Shopkeepers
            .AsNoTracking()
            .Select(sk => new { sk.Id, sk.AccountId, sk.Name })
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

        var accountIds = shopkeepers.Select(sk => sk.AccountId).ToList();
        var walletBalances = await _context.Wallets
            .AsNoTracking()
            .Where(w => accountIds.Contains(w.AccountId))
            .ToDictionaryAsync(w => w.AccountId, w => w.Balance, cancellationToken);

        var shopIdToKeeper = shops.ToDictionary(s => s.Id, s => s.ShopkeeperId);
        var shopIds = shopIdToKeeper.Keys.ToList();

        var sellerOrderRows = shopIds.Count == 0
            ? []
            : await _context.SellerOrders
                .AsNoTracking()
                .Where(so => shopIds.Contains(so.ShopId))
                .Select(so => new { so.Id, so.ShopId })
                .ToListAsync(cancellationToken);

        var orderIdToKeeper = sellerOrderRows.ToDictionary(
            r => r.Id,
            r => shopIdToKeeper[r.ShopId]);

        var sellerOrderIds = sellerOrderRows.Select(r => r.Id).ToList();
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

        var items = shopkeepers
            .OrderBy(sk => sk.Name)
            .Select(sk => new
            {
                accountId = sk.AccountId,
                shopkeeperName = sk.Name,
                shopName = shopNameByKeeper.GetValueOrDefault(sk.Id),
                walletBalance = walletBalances.GetValueOrDefault(sk.AccountId, 0m),
                pendingEarnings = pendingByKeeper.GetValueOrDefault(sk.Id, 0m),
                availableEarnings = availableByKeeper.GetValueOrDefault(sk.Id, 0m)
            })
            .ToList();

        return Ok(new { items });
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
