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

    public UpdatePartnerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
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

        if (!string.IsNullOrWhiteSpace(request.Permissions))
            partner.Permissions = request.Permissions;

        partner.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}