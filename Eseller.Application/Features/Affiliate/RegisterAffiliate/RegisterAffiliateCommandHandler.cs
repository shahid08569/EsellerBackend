using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.RegisterAffiliate;

public class RegisterAffiliateCommandHandler
    : IRequestHandler<RegisterAffiliateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;

    public RegisterAffiliateCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTime dateTime)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        RegisterAffiliateCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check email exists
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
            RoleType = RoleType.Affiliate,
            IsActive = true
        };

        account.SetCreatedAt(now);
        account.SetUpdatedAt(now);

        _context.Accounts.Add(account);

        // Generate unique referral code
        var referralCode = await GenerateUniqueReferralCode(cancellationToken);

        // Create Affiliate
        var affiliate = new Eseller.Domain.Entities.Identity.Affiliate
        {
            AccountId = account.Id,
            ReferralCode = referralCode,
            TotalClicks = 0,
            TotalConversions = 0,
            TotalEarned = 0,
            IsActive = true
        };

        affiliate.SetCreatedAt(now);
        affiliate.SetUpdatedAt(now);

        _context.Affiliates.Add(affiliate);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(affiliate.Id);
    }

    private async Task<string> GenerateUniqueReferralCode(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            var exists = await _context.Affiliates
                .AnyAsync(a => a.ReferralCode == code, cancellationToken);

            if (!exists)
                return code;
        }

        return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
    }
}