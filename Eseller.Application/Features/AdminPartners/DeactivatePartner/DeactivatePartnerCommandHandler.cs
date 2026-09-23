using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminPartners.DeactivatePartner;

public class DeactivatePartnerCommandHandler
    : IRequestHandler<DeactivatePartnerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeactivatePartnerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeactivatePartnerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin)
            return Result.Failure(
                "Only SuperAdmin can deactivate partners.", "FORBIDDEN");

        var partner = await _context.Partners
            .FirstOrDefaultAsync(p => p.Id == request.PartnerId, cancellationToken);

        if (partner is null)
            return Result.Failure("Partner not found.", "PARTNER_NOT_FOUND");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == partner.AccountId, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        if (!account.IsActive)
            return Result.Failure("Partner is already inactive.", "ALREADY_INACTIVE");

        var now = _dateTime.UtcNow;

        // ✅ Account deactivate karo (Partner entity mein IsActive nahi)
        account.IsActive = false;
        account.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}