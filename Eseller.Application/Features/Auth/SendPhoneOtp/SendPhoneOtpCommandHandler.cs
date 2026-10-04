using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.SendPhoneOtp;

public class SendPhoneOtpCommandHandler : IRequestHandler<SendPhoneOtpCommand, Result<SendPhoneOtpResponse>>
{
    private const int MaxAttemptsPerHour = 5;

    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly ISmsService _smsService;
    private readonly IEmailService _emailService;

    public SendPhoneOtpCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService,
        ISmsService smsService,
        IEmailService emailService)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
        _smsService = smsService;
        _emailService = emailService;
    }

    public async Task<Result<SendPhoneOtpResponse>> Handle(SendPhoneOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email.ToLower() == email, cancellationToken);

        if (account is null)
            return Result.Failure<SendPhoneOtpResponse>("Account not found.", "ACCOUNT_NOT_FOUND");

        if (account.IsPhoneVerified)
            return Result.Failure<SendPhoneOtpResponse>("Phone is already verified.", "PHONE_ALREADY_VERIFIED");

        string? phone = null;

        switch (account.RoleType)
        {
            case RoleType.User:
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);
                phone = user?.Phone;
                break;
            }
            case RoleType.Shopkeeper:
            {
                var keeper = await _context.Shopkeepers
                    .FirstOrDefaultAsync(sk => sk.AccountId == account.Id, cancellationToken);
                phone = keeper?.Phone;
                if (string.IsNullOrWhiteSpace(phone) && keeper is not null)
                {
                    phone = await _context.Shops
                        .IgnoreQueryFilters()
                        .Where(s => s.ShopkeeperId == keeper.Id && s.DeletedAt == null)
                        .Select(s => s.Phone)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(phone))
            return Result.Failure<SendPhoneOtpResponse>(
                "No phone number on this account. Save your phone first, then request OTP.",
                "PHONE_NOT_FOUND");

        var recentAttempts = await _verificationTokenService.GetRecentAttemptsCountAsync(
            account.Id,
            TokenPurpose.PhoneOtp,
            TimeSpan.FromHours(1),
            cancellationToken);

        if (recentAttempts >= MaxAttemptsPerHour)
            return Result.Failure<SendPhoneOtpResponse>(
                "Too many OTP attempts. Please try again later.",
                "RATE_LIMIT_EXCEEDED");

        var code = await _verificationTokenService.CreateTokenAsync(
            account.Id,
            TokenPurpose.PhoneOtp,
            VerificationChannel.Sms,
            expiryMinutes: 10,
            cancellationToken);

        await _smsService.SendOtpAsync(phone, code, cancellationToken);

        var masked = MaskPhone(phone);
        var mock = _smsService.IsMock;

        // Dev/mock SMS: also email the OTP so testers don't need console logs.
        if (mock)
        {
            try
            {
                var subject = $"{code} is your Eseller phone verification code";
                var text = $"Your Eseller phone OTP for {masked} is: {code}\n\nThis code expires in 10 minutes.";
                var html = $@"
                    <div style=""font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px;"">
                      <h2 style=""color:#111827;margin:0 0 12px;"">Phone verification</h2>
                      <p style=""color:#4b5563;font-size:14px;"">Your OTP for {masked} is:</p>
                      <p style=""font-size:32px;font-weight:900;letter-spacing:6px;color:#E11D48;font-family:monospace;"">{code}</p>
                      <p style=""color:#6b7280;font-size:12px;"">Expires in 10 minutes. (Dev/mock SMS — real SMS provider not configured.)</p>
                    </div>";
                await _emailService.SendAsync(account.Email, subject, html, text, cancellationToken);
            }
            catch
            {
                // Best-effort — DebugOtp still returned in API response
            }
        }

        return Result.Success(new SendPhoneOtpResponse(
            mock
                ? $"OTP generated for {masked}. Check your email inbox (dev/mock SMS)."
                : $"OTP sent to {masked}.",
            masked,
            mock ? code : null));
    }

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "****";
        return $"***{digits[^4..]}";
    }
}
