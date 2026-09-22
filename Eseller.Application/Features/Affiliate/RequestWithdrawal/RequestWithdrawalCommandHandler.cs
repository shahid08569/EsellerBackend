using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.RequestWithdrawal;

public class RequestWithdrawalCommandHandler
    : IRequestHandler<RequestWithdrawalCommand, Result<Guid>>
{
    private const string MinimumWithdrawalSettingKey = "AffiliateMinimumWithdrawal";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public RequestWithdrawalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        RequestWithdrawalCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<Guid>("Only affiliates can request withdrawal.", "FORBIDDEN");

        var accountId = _currentUserService.AccountId.Value;

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

        if (affiliate is null)
            return Result.Failure<Guid>("Affiliate not found.", "AFFILIATE_NOT_FOUND");

        if (!affiliate.IsActive)
            return Result.Failure<Guid>("Affiliate account is not active.", "AFFILIATE_INACTIVE");

        // Minimum withdrawal setting
        var minSetting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == MinimumWithdrawalSettingKey, cancellationToken);

        decimal minimumAmount = 1000m;

        if (minSetting is not null && decimal.TryParse(minSetting.Value, out var parsed))
            minimumAmount = parsed;

        if (request.Amount < minimumAmount)
            return Result.Failure<Guid>(
                $"Minimum withdrawal amount is Rs {minimumAmount:N0}.",
                "BELOW_MINIMUM");

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);

        if (wallet is null || wallet.Balance < request.Amount)
            return Result.Failure<Guid>("Insufficient balance.", "INSUFFICIENT_BALANCE");

        var hasPending = await _context.Withdrawals
            .AnyAsync(w => w.AccountId == accountId
                        && w.Status == WithdrawalStatus.Pending,
                cancellationToken);

        if (hasPending)
            return Result.Failure<Guid>(
                "You already have a pending withdrawal request.",
                "PENDING_WITHDRAWAL_EXISTS");

        var now = _dateTime.UtcNow;

        var withdrawal = new Withdrawal
        {
            AccountId = accountId,
            Amount = request.Amount,
            PayoutMethod = request.PayoutMethod.Trim(),
            Status = WithdrawalStatus.Pending,
            RequestedAt = now
        };

        //AuditableEntity — Set methods
        withdrawal.SetCreatedAt(now);
        withdrawal.SetUpdatedAt(now);

        _context.Withdrawals.Add(withdrawal);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(withdrawal.Id);
    }
}