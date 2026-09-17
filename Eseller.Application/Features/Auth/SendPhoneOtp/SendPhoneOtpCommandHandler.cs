using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.SendPhoneOtp;

public class SendPhoneOtpCommandHandler : IRequestHandler<SendPhoneOtpCommand, Result>
{
    private const int MaxAttemptsPerHour = 3;

    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly ISmsService _smsService;

    public SendPhoneOtpCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService,
        ISmsService smsService)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
        _smsService = smsService;
    }

    public async Task<Result> Handle(SendPhoneOtpCommand request, CancellationToken cancellationToken)
    {
        // 1. Find account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        if (account.IsPhoneVerified)
            return Result.Failure("Phone is already verified.", "PHONE_ALREADY_VERIFIED");

        // 2. Get phone number — role ke hisaab se
        string? phone = null;

        switch (account.RoleType)
        {
            case RoleType.User:
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);
                phone = user?.Phone;
                break;

            case RoleType.Shopkeeper:
                // Shopkeeper table mein phone nahi — account se nahi mila
                // Account mein phone nahi hai — skip
                break;

            case RoleType.Affiliate:
                // Affiliate table mein phone nahi
                break;
        }

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure("No phone number found for this account.", "PHONE_NOT_FOUND");

        // 3. Rate limit check
        var recentAttempts = await _verificationTokenService.GetRecentAttemptsCountAsync(
            account.Id,
            TokenPurpose.PhoneOtp,
            TimeSpan.FromHours(1),
            cancellationToken);

        if (recentAttempts >= MaxAttemptsPerHour)
            return Result.Failure(
                "Too many OTP attempts. Please try again later.",
                "RATE_LIMIT_EXCEEDED");

        // 4. Generate OTP
        var code = await _verificationTokenService.CreateTokenAsync(
            account.Id,
            TokenPurpose.PhoneOtp,
            VerificationChannel.Sms,
            expiryMinutes: 10,   // Will refine to 10 minutes below
            cancellationToken);

        // 5. Send SMS
        await _smsService.SendOtpAsync(phone, code, cancellationToken);

        return Result.Success();
    }
}