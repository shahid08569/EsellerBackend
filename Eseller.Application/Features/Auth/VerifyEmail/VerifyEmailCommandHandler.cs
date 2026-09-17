using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;

    public VerifyEmailCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
    }

    public async Task<Result> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find account by email
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        // 2. Already verified?
        if (account.IsEmailVerified)
            return Result.Failure("Email is already verified.", "EMAIL_ALREADY_VERIFIED");

        // 3. Validate token
        var isValid = await _verificationTokenService.ValidateTokenAsync(
            account.Id,
            TokenPurpose.EmailVerification,
            request.Code,
            cancellationToken);

        if (!isValid)
            return Result.Failure("Invalid or expired verification code.", "INVALID_VERIFICATION_CODE");

        // 4. Mark email verified
        account.IsEmailVerified = true;
        account.SetUpdatedAt(DateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}