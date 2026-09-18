using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
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

        // 3. Check Store URL uniqueness
        var storeUrlExists = await _context.Shops
            .AnyAsync(s => s.Slug == request.StoreUrl, cancellationToken);

        if (storeUrlExists)
            return Result.Failure<RegisterSellerResponse>(
                "Store URL is already taken.",
                "STORE_URL_TAKEN");

        // 4. Create Account
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

        // 5. Create Shopkeeper (Pending approval)
        var shopkeeper = new Shopkeeper
        {
            AccountId = account.Id,
            Name = request.Name,
            Phone = request.Phone,
            Status = ShopkeeperApprovalStatus.Pending
        };

        _context.Shopkeepers.Add(shopkeeper);

        // 6. Create Shop (Inactive — wait for approval)
        var shop = new Shop
        {
            ShopkeeperId = shopkeeper.Id,
            Name = request.StoreName,
            Slug = request.StoreUrl,
            Description = request.StoreDescription,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            IsActive = false,
            Rating = 0,
            TotalProducts = 0
        };

        _context.Shops.Add(shop);

        // 7. Save ALL changes
        await _context.SaveChangesAsync(cancellationToken);

        // 8. Generate verification token
        var code = await _verificationTokenService.CreateTokenAsync(
            account.Id,
            TokenPurpose.EmailVerification,
            VerificationChannel.Email,
            expiryMinutes: 60 * 24,
            cancellationToken);

        // 9. Send verification email
        await _emailService.SendVerificationEmailAsync(account.Email, code, cancellationToken);

        // 10. Log location
        await _locationService.LogLocationAsync(
            account.Id,
            request.Latitude,
            request.Longitude,
            request.DeviceType,
            "RegisterSeller",
            null,
            null,
            cancellationToken);

        // 11. Return
        return Result.Success(new RegisterSellerResponse(
            account.Id,
            shopkeeper.Id,
            shop.Id,
            request.Name,
            account.Email,
            shop.Name,
            shop.Slug,
            "Pending",
            "Your application is under review. You will be notified once approved."));
    }
}