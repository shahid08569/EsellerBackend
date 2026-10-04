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
    private readonly ISignalRNotificationService _signalR;
    private readonly IDateTime _dateTime;

    public RegisterSellerCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILocationService locationService,
        ISignalRNotificationService signalR,
        IDateTime dateTime)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _locationService = locationService;
        _signalR = signalR;
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

        // 9. Pending approval notice for the new seller + alert admins
        var now = _dateTime.UtcNow;
        var sellerNotif = new Notification
        {
            AccountId = account.Id,
            Type = NotificationType.SystemAlert,
            Title = "Approval pending",
            Message = "Your seller account is under review. You cannot sell until Super Admin approves your store.",
            IsRead = false
        };
        sellerNotif.SetCreatedAt(now);
        sellerNotif.SetUpdatedAt(now);
        _context.Notifications.Add(sellerNotif);

        var adminIds = await _context.Accounts
            .Where(a => a.RoleType == RoleType.SuperAdmin || a.RoleType == RoleType.Partner)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var adminNotifs = new List<Notification>();
        foreach (var adminId in adminIds)
        {
            var n = new Notification
            {
                AccountId = adminId,
                Type = NotificationType.NewSellerRegistration,
                Title = "New seller registration",
                Message = $"\"{shop.Name}\" ({account.Email}) applied and is awaiting Super Admin approval.",
                IsRead = false
            };
            n.SetCreatedAt(now);
            n.SetUpdatedAt(now);
            adminNotifs.Add(n);
            _context.Notifications.Add(n);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _signalR.SendNotificationToAccountAsync(
            account.Id, sellerNotif.Id, sellerNotif.Type, sellerNotif.Title, sellerNotif.Message, cancellationToken);

        foreach (var n in adminNotifs)
        {
            await _signalR.SendNotificationToAccountAsync(
                n.AccountId, n.Id, n.Type, n.Title, n.Message, cancellationToken);
        }

        // 12. Return
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