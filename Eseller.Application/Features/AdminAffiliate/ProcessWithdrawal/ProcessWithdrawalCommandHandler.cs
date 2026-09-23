using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.ProcessWithdrawal;

public class ProcessWithdrawalCommandHandler
    : IRequestHandler<ProcessWithdrawalCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ProcessWithdrawalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        ProcessWithdrawalCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can process withdrawals.", "FORBIDDEN");

        var withdrawal = await _context.Withdrawals
            .FirstOrDefaultAsync(w => w.Id == request.WithdrawalId, cancellationToken);

        if (withdrawal is null)
            return Result.Failure("Withdrawal not found.", "WITHDRAWAL_NOT_FOUND");

        if (withdrawal.Status != WithdrawalStatus.Approved)
            return Result.Failure(
                "Only approved withdrawals can be processed.",
                "INVALID_STATUS");

        var now = _dateTime.UtcNow;

        // Wallet debit
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == withdrawal.AccountId, cancellationToken);

        if (wallet is null)
            return Result.Failure("Wallet not found.", "WALLET_NOT_FOUND");

        if (wallet.Balance < withdrawal.Amount)
            return Result.Failure(
                "Insufficient wallet balance.",
                "INSUFFICIENT_BALANCE");

        wallet.Balance -= withdrawal.Amount;
        wallet.SetUpdatedAt(now);

        // Wallet transaction
        _context.WalletTransactions.Add(new Eseller.Domain.Entities.Wallet.WalletTransaction
        {
            WalletId = wallet.Id,
            Direction = WalletDirection.Debit,
            TransactionType = WalletTransactionType.Withdrawal,
            Amount = withdrawal.Amount,
            ReferenceType = "Withdrawal",
            ReferenceId = withdrawal.Id,
            Description = $"Withdrawal processed — {withdrawal.PayoutMethod}",
            CreatedAt = now
        });

        // Update withdrawal
        withdrawal.Status = WithdrawalStatus.Paid;
        withdrawal.ProcessedAt = now;
        withdrawal.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}