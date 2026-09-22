using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.RejectAffiliateCommission;

public class RejectAffiliateCommissionCommandHandler
    : IRequestHandler<RejectAffiliateCommissionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public RejectAffiliateCommissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        RejectAffiliateCommissionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can reject commissions.", "FORBIDDEN");

        var commission = await _context.AffiliateCommissions
            .FirstOrDefaultAsync(c => c.Id == request.CommissionId, cancellationToken);

        if (commission is null)
            return Result.Failure("Commission not found.", "COMMISSION_NOT_FOUND");

        if (commission.Status != AffiliateCommissionStatus.Pending)
            return Result.Failure(
                "Only pending commissions can be rejected.",
                "INVALID_STATUS");

        commission.Status = AffiliateCommissionStatus.Reversed;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}