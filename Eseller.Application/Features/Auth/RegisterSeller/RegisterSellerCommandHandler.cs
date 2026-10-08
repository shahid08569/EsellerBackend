using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Wallet;
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
    private readonly IAppNotificationService _notifications;
    private readonly IDateTime _dateTime;

    public RegisterSellerCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILocationService locationService,
        IAppNotificationService notifications,
        IDateTime dateTime)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _locationService = locationService;
        _notifications = notifications;
        _dateTime = dateTime;
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

        // 3b. Reject fake / failed local document placeholders
        if (IsFakeDocumentUrl(request.CnicFrontUrl) ||
            IsFakeDocumentUrl(request.CnicBackUrl) ||
            IsFakeDocumentUrl(request.DocumentUrl))
        {
            return Result.Failure<RegisterSellerResponse>(
                "Identity document upload failed. Please re-upload your CNIC/document images and try again.",
                "DOCUMENT_UPLOAD_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(request.CnicFrontUrl) && string.IsNullOrWhiteSpace(request.DocumentUrl))
        {
            return Result.Failure<RegisterSellerResponse>(
                "Front side of your identity document is required.",
                "DOCUMENT_UPLOAD_REQUIRED");
        }

        // 4. Create Account
        var account = new Account
        {
            Username = request.Email,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleType = RoleType.Shopkeeper,
            IsActive = true,
            // No email OTP — SuperAdmin approves the seller store.
            IsEmailVerified = true,
            IsPhoneVerified = false
        };

        _context.Accounts.Add(account);

        // Seller payout wallet (credited when orders are Delivered)
        var wallet = new Wallet
        {
            AccountId = account.Id,
            Balance = 0
        };
        wallet.SetCreatedAt(_dateTime.UtcNow);
        wallet.SetUpdatedAt(_dateTime.UtcNow);
        _context.Wallets.Add(wallet);

        // 5. Create Shopkeeper (Pending approval)
        var shopkeeper = new Shopkeeper
        {
            AccountId = account.Id,
            Name = request.Name,
            Phone = request.Phone,
            DocumentType = request.DocumentType,
            DocumentNumber = request.DocumentNumber,
            CnicFrontUrl = request.CnicFrontUrl,
            CnicBackUrl = request.CnicBackUrl,
            DocumentUrl = request.DocumentUrl,
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

        // 8. Log location (no verification email — SuperAdmin approval only)
        await _locationService.LogLocationAsync(
            account.Id,
            request.Latitude,
            request.Longitude,
            request.DeviceType,
            "RegisterSeller",
            null,
            null,
            cancellationToken);

        // 8b. Track referral if registered with an Invitation / Referral Code
        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            var normalizedRef = request.ReferralCode.Trim().ToUpperInvariant();
            var referrerAffiliate = await _context.Affiliates
                .FirstOrDefaultAsync(a => a.ReferralCode == normalizedRef, cancellationToken);

            if (referrerAffiliate != null)
            {
                referrerAffiliate.TotalConversions += 1;
                referrerAffiliate.SetUpdatedAt(_dateTime.UtcNow);

                _context.AffiliateClicks.Add(new Eseller.Domain.Entities.Affiliate.AffiliateClick
                {
                    AffiliateId = referrerAffiliate.Id,
                    ProductId = shop.Id,
                    ClickedAt = _dateTime.UtcNow,
                    IpAddress = null,
                    UserAgent = $"SellerJoined:{shop.Name}",
                    Converted = true
                });

                await _context.SaveChangesAsync(cancellationToken);

                try
                {
                    await _notifications.NotifyAsync(
                        referrerAffiliate.AccountId,
                        RoleType.Shopkeeper,
                        NotificationType.NewSeller,
                        "New Store Joined via Your Referral!",
                        $"\"{shop.Name}\" just registered using your invitation link.",
                        NotificationEntityType.Seller,
                        shop.Id,
                        "/referrals",
                        $"ReferralJoined:{shop.Id:N}",
                        cancellationToken);
                }
                catch { /* best-effort */ }
            }
        }

        await _notifications.NotifyAdminsAsync(
            NotificationType.NewSeller,
            "New Seller Registered",
            $"A new seller has registered: \"{shop.Name}\" ({account.Email}).",
            NotificationEntityType.Seller,
            shopkeeper.Id,
            "/sellers",
            $"NewSeller:{shopkeeper.Id:N}",
            cancellationToken);

        return Result.Success(new RegisterSellerResponse(
            account.Id,
            shopkeeper.Id,
            shop.Id,
            request.Name,
            account.Email,
            shop.Name,
            shop.Slug,
            "Pending",
            "Your application is under Super Admin review. No email verification is required."));
    }

    private static bool IsFakeDocumentUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();
        return trimmed.StartsWith("local-front:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("local-back:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("local-", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }
}