using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.ApproveWithdrawal;

public class ApproveWithdrawalCommandHandler
    : IRequestHandler<ApproveWithdrawalCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApproveWithdrawalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        ApproveWithdrawalCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can approve withdrawals.", "FORBIDDEN");

        var withdrawal = await _context.Withdrawals
            .FirstOrDefaultAsync(w => w.Id == request.WithdrawalId, cancellationToken);

        if (withdrawal is null)
            return Result.Failure("Withdrawal not found.", "WITHDRAWAL_NOT_FOUND");

        if (withdrawal.Status != WithdrawalStatus.Pending)
            return Result.Failure(
                "Only pending withdrawals can be approved.",
                "INVALID_STATUS");

        var now = _dateTime.UtcNow;

        withdrawal.Status = WithdrawalStatus.Approved;
        withdrawal.ProcessedAt = now;
        withdrawal.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}