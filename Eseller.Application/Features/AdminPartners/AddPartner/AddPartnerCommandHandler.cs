using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminPartners.AddPartner;

public class AddPartnerCommandHandler
    : IRequestHandler<AddPartnerCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;

    public AddPartnerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        AddPartnerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin)
            return Result.Failure<Guid>(
                "Only SuperAdmin can add partners.", "FORBIDDEN");

        // Enforce maximum 3 partners rule
        var activePartnersCount = await _context.Partners
            .Join(_context.Accounts, p => p.AccountId, a => a.Id, (p, a) => new { p, a })
            .CountAsync(x => x.a.IsActive, cancellationToken);

        if (activePartnersCount >= 3)
        {
            return Result.Failure<Guid>(
                "Maximum limit of 3 partners reached. You cannot add more than 3 active partners.",
                "MAX_PARTNERS_LIMIT_REACHED");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _context.Accounts
            .AnyAsync(a => a.Email == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result.Failure<Guid>("Email is already registered.", "EMAIL_EXISTS");

        var now = _dateTime.UtcNow;

        // Create Account
        var account = new Account
        {
            Email = normalizedEmail,
            Username = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleType = RoleType.Partner,
            IsActive = true
        };

        account.SetCreatedAt(now);
        account.SetUpdatedAt(now);

        _context.Accounts.Add(account);

        // Create Partner
        var partner = new Partner
        {
            AccountId = account.Id,
            AddedBySuperAdminId = _currentUserService.AccountId.Value,
            AddedAt = now,
            Permissions = request.Permissions
        };

        partner.SetCreatedAt(now);
        partner.SetUpdatedAt(now);

        _context.Partners.Add(partner);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(partner.Id);
    }
}