using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.ResendVerificationEmail;

public class ResendVerificationEmailCommandHandler
    : IRequestHandler<ResendVerificationEmailCommand, Result>
{
    private const int MaxAttemptsPerHour = 3;

    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly IEmailService _emailService;

    public ResendVerificationEmailCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService,
        IEmailService emailService)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
        _emailService = emailService;
    }

    public async Task<Result> Handle(
        ResendVerificationEmailCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        if (account.IsEmailVerified)
            return Result.Failure("Email is already verified.", "EMAIL_ALREADY_VERIFIED");

        // 2. Rate limit check
        var recentAttempts = await _verificationTokenService.GetRecentAttemptsCountAsync(
            account.Id,
            TokenPurpose.EmailVerification,
            TimeSpan.FromHours(1),
            cancellationToken);

        if (recentAttempts >= MaxAttemptsPerHour)
            return Result.Failure(
                "Too many verification attempts. Please try again later.",
                "RATE_LIMIT_EXCEEDED");

        // 3. Generate new code
        var code = await _verificationTokenService.CreateTokenAsync(
            account.Id,
            TokenPurpose.EmailVerification,
            VerificationChannel.Email,
            expiryMinutes: 60 * 24,
            cancellationToken);

        // 4. Send email
        await _emailService.SendVerificationEmailAsync(account.Email, code, cancellationToken);

        return Result.Success();
    }
}