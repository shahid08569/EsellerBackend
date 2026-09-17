using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.VerifyPhoneOtp;

public class VerifyPhoneOtpCommandHandler : IRequestHandler<VerifyPhoneOtpCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly IDateTime _dateTime;

    public VerifyPhoneOtpCommandHandler(
        IApplicationDbContext context,
        IVerificationTokenService verificationTokenService,
        IDateTime dateTime)
    {
        _context = context;
        _verificationTokenService = verificationTokenService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(VerifyPhoneOtpCommand request, CancellationToken cancellationToken)
    {
        // 1. Find account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == request.Email, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        if (account.IsPhoneVerified)
            return Result.Failure("Phone is already verified.", "PHONE_ALREADY_VERIFIED");

        // 2. Validate token
        var isValid = await _verificationTokenService.ValidateTokenAsync(
            account.Id,
            TokenPurpose.PhoneOtp,
            request.Code,
            cancellationToken);

        if (!isValid)
            return Result.Failure("Invalid or expired OTP code.", "INVALID_OTP");

        // 3. Mark phone verified
        account.IsPhoneVerified = true;
        account.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}