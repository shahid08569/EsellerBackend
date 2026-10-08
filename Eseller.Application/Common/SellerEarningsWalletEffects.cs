using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Common;

/// <summary>
/// Keeps Wallet.PendingEarnings / AvailableEarnings / Balance in sync with SellerEarnings
/// across place → deliver → cancel/refund/delete. SuperAdmin Adjust writes wallet directly.
/// </summary>
public static class SellerEarningsWalletEffects
{
    public static decimal AmountOf(SellerEarning e) =>
        e.NetAmount > 0 ? e.NetAmount : e.CommissionAmount;

    /// <summary>Close Pending earnings and decrease Wallet.PendingEarnings (cancel before delivery).</summary>
    public static async Task ReversePendingAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        IReadOnlyCollection<Guid> sellerOrderIds,
        CancellationToken cancellationToken)
    {
        if (sellerOrderIds.Count == 0) return;

        var earnings = await context.SellerEarnings
            .Where(se => sellerOrderIds.Contains(se.SellerOrderId)
                      && se.Status == SellerEarningStatus.Pending)
            .ToListAsync(cancellationToken);

        if (earnings.Count == 0) return;

        var accountBySellerOrder = await ResolveAccountBySellerOrderAsync(
            context, sellerOrderIds, cancellationToken);

        var pendingByAccount = new Dictionary<Guid, decimal>();
        foreach (var earning in earnings)
        {
            earning.Status = SellerEarningStatus.Withdrawn; // closed / not payable
            if (!accountBySellerOrder.TryGetValue(earning.SellerOrderId, out var accountId))
                continue;
            var amount = AmountOf(earning);
            pendingByAccount[accountId] = pendingByAccount.GetValueOrDefault(accountId) + amount;
        }

        var now = dateTime.UtcNow;
        foreach (var (accountId, amount) in pendingByAccount)
        {
            var wallet = await context.Wallets
                .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);
            if (wallet is null) continue;
            wallet.PendingEarnings = Math.Max(0m, wallet.PendingEarnings - amount);
            wallet.SetUpdatedAt(now);
        }
    }

    /// <summary>
    /// Close Available earnings and claw back AvailableEarnings + Balance (refund after delivery).
    /// </summary>
    public static async Task ClawBackAvailableAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        IReadOnlyCollection<Guid> sellerOrderIds,
        string description,
        CancellationToken cancellationToken)
    {
        if (sellerOrderIds.Count == 0) return;

        var earnings = await context.SellerEarnings
            .Where(se => sellerOrderIds.Contains(se.SellerOrderId)
                      && se.Status == SellerEarningStatus.Available)
            .ToListAsync(cancellationToken);

        if (earnings.Count == 0) return;

        var accountBySellerOrder = await ResolveAccountBySellerOrderAsync(
            context, sellerOrderIds, cancellationToken);

        var now = dateTime.UtcNow;
        foreach (var earning in earnings)
        {
            earning.Status = SellerEarningStatus.Withdrawn;
            if (!accountBySellerOrder.TryGetValue(earning.SellerOrderId, out var accountId))
                continue;

            var amount = AmountOf(earning);
            var wallet = await context.Wallets
                .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);
            if (wallet is null) continue;

            wallet.AvailableEarnings = Math.Max(0m, wallet.AvailableEarnings - amount);
            wallet.Balance = Math.Max(0m, wallet.Balance - amount);
            wallet.SetUpdatedAt(now);

            context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Direction = WalletDirection.Debit,
                TransactionType = WalletTransactionType.Refund,
                Amount = amount,
                ReferenceType = "SellerOrder",
                ReferenceId = earning.SellerOrderId,
                Description = description,
                CreatedAt = now
            });
        }
    }

    /// <summary>
    /// Before deleting SellerEarnings rows, undo wallet counters for Pending / Available.
    /// </summary>
    public static async Task UndoWalletBeforeDeleteAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        IReadOnlyList<SellerEarning> earnings,
        IReadOnlyCollection<Guid> sellerOrderIds,
        CancellationToken cancellationToken)
    {
        if (earnings.Count == 0) return;

        var accountBySellerOrder = await ResolveAccountBySellerOrderAsync(
            context, sellerOrderIds, cancellationToken);

        var now = dateTime.UtcNow;
        foreach (var earning in earnings)
        {
            if (!accountBySellerOrder.TryGetValue(earning.SellerOrderId, out var accountId))
                continue;

            var amount = AmountOf(earning);
            var wallet = await context.Wallets
                .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);
            if (wallet is null) continue;

            if (earning.Status == SellerEarningStatus.Pending)
            {
                wallet.PendingEarnings = Math.Max(0m, wallet.PendingEarnings - amount);
                wallet.SetUpdatedAt(now);
            }
            else if (earning.Status == SellerEarningStatus.Available)
            {
                wallet.AvailableEarnings = Math.Max(0m, wallet.AvailableEarnings - amount);
                wallet.Balance = Math.Max(0m, wallet.Balance - amount);
                wallet.SetUpdatedAt(now);

                context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Direction = WalletDirection.Debit,
                    TransactionType = WalletTransactionType.ManualAdjustment,
                    Amount = amount,
                    ReferenceType = "OrderDeleted",
                    ReferenceId = earning.SellerOrderId,
                    Description = "Seller earning reversed because order was deleted",
                    CreatedAt = now
                });
            }
        }
    }

    private static async Task<Dictionary<Guid, Guid>> ResolveAccountBySellerOrderAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> sellerOrderIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from so in context.SellerOrders.AsNoTracking()
            join s in context.Shops.IgnoreQueryFilters().AsNoTracking() on so.ShopId equals s.Id
            join sk in context.Shopkeepers.AsNoTracking() on s.ShopkeeperId equals sk.Id
            where sellerOrderIds.Contains(so.Id)
            select new { so.Id, sk.AccountId }
        ).ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => r.AccountId);
    }
}
