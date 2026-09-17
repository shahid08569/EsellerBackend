using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private const int MaxAttemptsPerHour = 3;

    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService,
        IEmailService emailService)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
        _emailService = emailService;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // 1. Find account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        // ⚠️ Security — agar account nahi mila, phir bhi Success return karo
        // (taake attacker ko pata na chale ke email exist karta hai ya nahi)
        if (account is null)
            return Result.Success();

        // 2. Rate limit
        var recentAttempts = await _verificationTokenService.GetRecentAttemptsCountAsync(
            account.Id,
            TokenPurpose.PasswordReset,
            TimeSpan.FromHours(1),
            cancellationToken);

        if (recentAttempts >= MaxAttemptsPerHour)
            return Result.Failure(
                "Too many password reset attempts. Please try again later.",
                "RATE_LIMIT_EXCEEDED");

        // 3. Generate token
        var code = await _verificationTokenService.CreateTokenAsync(
            account.Id,
            TokenPurpose.PasswordReset,
            VerificationChannel.Email,
            expiryMinutes: 15,
            cancellationToken);

        // 4. Send email
        await _emailService.SendPasswordResetEmailAsync(account.Email, code, cancellationToken);

        return Result.Success();
    }
}