using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminPartners.ActivatePartner;

public class ActivatePartnerCommandHandler
    : IRequestHandler<ActivatePartnerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ActivatePartnerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        ActivatePartnerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin)
            return Result.Failure(
                "Only SuperAdmin can activate partners.", "FORBIDDEN");

        var partner = await _context.Partners
            .FirstOrDefaultAsync(p => p.Id == request.PartnerId, cancellationToken);

        if (partner is null)
            return Result.Failure("Partner not found.", "PARTNER_NOT_FOUND");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == partner.AccountId, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        if (account.IsActive)
            return Result.Failure("Partner is already active.", "ALREADY_ACTIVE");

        // Enforce maximum 3 active partners rule
        var activePartnersCount = await _context.Partners
            .Join(_context.Accounts, p => p.AccountId, a => a.Id, (p, a) => new { p, a })
            .CountAsync(x => x.a.IsActive, cancellationToken);

        if (activePartnersCount >= 3)
        {
            return Result.Failure(
                "Maximum limit of 3 active partners reached. You cannot activate more than 3 partners.",
                "MAX_PARTNERS_LIMIT_REACHED");
        }

        var now = _dateTime.UtcNow;
        account.IsActive = true;
        account.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
