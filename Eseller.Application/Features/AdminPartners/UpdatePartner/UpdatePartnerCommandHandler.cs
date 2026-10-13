using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminPartners.UpdatePartner;

public class UpdatePartnerCommandHandler
    : IRequestHandler<UpdatePartnerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IPasswordHasher _passwordHasher;

    public UpdatePartnerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result> Handle(
        UpdatePartnerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin)
            return Result.Failure("Only SuperAdmin can update partners.", "FORBIDDEN");

        var partner = await _context.Partners
            .FirstOrDefaultAsync(p => p.Id == request.PartnerId, cancellationToken);

        if (partner is null)
            return Result.Failure("Partner not found.", "PARTNER_NOT_FOUND");

        if (request.Permissions is not null)
            partner.Permissions = request.Permissions;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == partner.AccountId, cancellationToken);
            if (account != null)
            {
                account.PasswordHash = _passwordHasher.Hash(request.NewPassword.Trim());
                account.SetUpdatedAt(_dateTime.UtcNow);
            }
        }

        partner.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}