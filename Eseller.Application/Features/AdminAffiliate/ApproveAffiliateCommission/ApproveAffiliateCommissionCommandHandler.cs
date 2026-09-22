using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.ApproveAffiliateCommission;

public class ApproveAffiliateCommissionCommandHandler
    : IRequestHandler<ApproveAffiliateCommissionCommand, Result>
{
    private const string HoldDaysSettingKey = "AffiliateCommissionHoldDays";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApproveAffiliateCommissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        ApproveAffiliateCommissionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        // 2. Role
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can approve commissions.", "FORBIDDEN");

        // 3. Get commission
        var commission = await _context.AffiliateCommissions
            .FirstOrDefaultAsync(c => c.Id == request.CommissionId, cancellationToken);

        if (commission is null)
            return Result.Failure("Commission not found.", "COMMISSION_NOT_FOUND");

        // 4. Status check
        if (commission.Status != AffiliateCommissionStatus.Pending)
            return Result.Failure(
                "Only pending commissions can be approved.",
                "INVALID_STATUS");

        // 5. Delivered check
        var deliveredHistory = await _context.OrderStatusHistories
            .FirstOrDefaultAsync(h => h.OrderRequestId == commission.OrderRequestId
                                   && h.NewStatus == OrderStatus.Delivered,
                cancellationToken);

        if (deliveredHistory is null)
            return Result.Failure(
                "Order has not been delivered yet.",
                "ORDER_NOT_DELIVERED");

        // 6. Hold days check
        var now = _dateTime.UtcNow;

        var holdDaysSetting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == HoldDaysSettingKey, cancellationToken);

        int holdDays = 7;
        if (holdDaysSetting is not null && int.TryParse(holdDaysSetting.Value, out var parsed))
            holdDays = parsed;

        var availableAt = deliveredHistory.ChangedAt.AddDays(holdDays);

        if (availableAt > now)
        {
            var daysLeft = Math.Ceiling((availableAt - now).TotalDays);
            return Result.Failure(
                $"Return window is still active. {daysLeft} day(s) remaining.",
                "RETURN_WINDOW_ACTIVE");
        }

        // 7. Get affiliate
        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.Id == commission.AffiliateId, cancellationToken);

        if (affiliate is null)
            return Result.Failure("Affiliate not found.", "AFFILIATE_NOT_FOUND");

        // 8. Approve commission
        commission.Status = AffiliateCommissionStatus.Approved;
        commission.ApprovedAt = now;

        // 9. Update affiliate TotalEarned
        affiliate.TotalEarned += commission.CommissionAmount;
        affiliate.SetUpdatedAt(now);

        // 10. Wallet credit
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == affiliate.AccountId, cancellationToken);

        if (wallet is null)
        {
            wallet = new Eseller.Domain.Entities.Wallet.Wallet
            {
                AccountId = affiliate.AccountId,
                Balance = 0m
            };

            wallet.SetCreatedAt(now);
            wallet.SetUpdatedAt(now);

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync(cancellationToken);
        }

        wallet.Balance += commission.CommissionAmount;
        wallet.SetUpdatedAt(now);

        // 11. Wallet transaction log
        _context.WalletTransactions.Add(new Eseller.Domain.Entities.Wallet.WalletTransaction
        {
            WalletId = wallet.Id,
            Direction = WalletDirection.Credit,
            TransactionType = WalletTransactionType.AffiliateCommission,
            Amount = commission.CommissionAmount,
            ReferenceType = "OrderRequest",
            ReferenceId = commission.OrderRequestId,
            Description = $"Affiliate commission approved for order {commission.OrderRequestId.ToString().Substring(0, 8)}",
            CreatedAt = now
        });

        // 12. Save
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}