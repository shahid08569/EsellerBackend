using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService,
        IPasswordHasher passwordHasher,
        IDateTime dateTime)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // 1. Find account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        if (account is null)
            return Result.Failure("Invalid or expired reset code.", "INVALID_RESET_CODE");

        // 2. Validate token
        var isValid = await _verificationTokenService.ValidateTokenAsync(
            account.Id,
            TokenPurpose.PasswordReset,
            request.Code,
            cancellationToken);

        if (!isValid)
            return Result.Failure("Invalid or expired reset code.", "INVALID_RESET_CODE");

        // 3. Update password
        account.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        account.SetUpdatedAt(_dateTime.UtcNow);

        // 4. Revoke all active refresh tokens (security)
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.AccountId == account.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(_dateTime.UtcNow);
        }

        // 5. Save
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}