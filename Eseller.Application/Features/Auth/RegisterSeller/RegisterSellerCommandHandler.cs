using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.RegisterSeller;

public class RegisterSellerCommandHandler
    : IRequestHandler<RegisterSellerCommand, Result<RegisterSellerResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILocationService _locationService;
    private readonly IVerificationTokenService _verificationTokenService;
    private readonly IEmailService _emailService;

    public RegisterSellerCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILocationService locationService,
        IVerificationTokenService verificationTokenService,
        IEmailService emailService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _locationService = locationService;
        _verificationTokenService = verificationTokenService;
        _emailService = emailService;
    }

    public async Task<Result<RegisterSellerResponse>> Handle(
        RegisterSellerCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate location
        if (!await _locationService.IsValidLocationAsync(
                request.Latitude, request.Longitude, cancellationToken))
        {
            return Result.Failure<RegisterSellerResponse>(
                "Valid location is required to register.",
                "LOCATION_REQUIRED");
        }

        // 2. Check email uniqueness
        var emailExists = await _context.Accounts
            .AnyAsync(a => a.Email == request.Email, cancellationToken);

        if (emailExists)
            return Result.Failure<RegisterSellerResponse>(
                "Email is already registered.",
                "EMAIL_TAKEN");

        // 3. Create Account
        var account = new Account
        {
            Username = request.Email,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleType = RoleType.Shopkeeper,
            IsActive = true,
            IsEmailVerified = false,
            IsPhoneVerified = false
        };

        _context.Accounts.Add(account);

        // 4. Create Shopkeeper
        _context.Shopkeepers.Add(new Shopkeeper
        {
            AccountId = account.Id,
            Name = request.Name,
            Status = ShopkeeperApprovalStatus.Pending
        });

        // 5. Save
        await _context.SaveChangesAsync(cancellationToken);

        // 6. Generate verification token
        var code = await _verificationTokenService.CreateTokenAsync(
            account.Id,
            TokenPurpose.EmailVerification,
            VerificationChannel.Email,
            expiryMinutes: 60 * 24,
            cancellationToken);

        // 7. Send verification email
        await _emailService.SendVerificationEmailAsync(account.Email, code, cancellationToken);

        // 8. Log location
        await _locationService.LogLocationAsync(
            account.Id,
            request.Latitude,
            request.Longitude,
            request.DeviceType,
            "RegisterSeller",
            null,
            null,
            cancellationToken);

        // 9. Return
        return Result.Success(new RegisterSellerResponse(
            account.Id,
            request.Name,
            account.Email,
            account.RoleType.ToString(),
            "Pending",
            "Your application is under review. You will be notified once approved."));
    }
}