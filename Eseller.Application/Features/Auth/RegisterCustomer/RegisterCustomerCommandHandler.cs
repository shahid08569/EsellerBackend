using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.RegisterCustomer;

public class RegisterCustomerCommandHandler
    : IRequestHandler<RegisterCustomerCommand, Result<RegisterCustomerResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILocationService _locationService;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public RegisterCustomerCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILocationService locationService,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _locationService = locationService;
        _dateTime = dateTime;
        _notifications = notifications;
    }

    public async Task<Result<RegisterCustomerResponse>> Handle(
        RegisterCustomerCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _locationService.IsValidLocationAsync(
                request.Latitude, request.Longitude, cancellationToken))
        {
            return Result.Failure<RegisterCustomerResponse>(
                "Valid location is required to register.",
                "LOCATION_REQUIRED");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.Name.Trim();
        var phone = request.Phone.Trim();

        var emailExists = await _context.Accounts
            .AnyAsync(a => a.Email.ToLower() == email || a.Username.ToLower() == email, cancellationToken);

        if (emailExists)
            return Result.Failure<RegisterCustomerResponse>(
                "Email is already registered.",
                "EMAIL_TAKEN");

        var account = new Account
        {
            Username = email,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleType = RoleType.User,
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = false
        };

        _context.Accounts.Add(account);

        var user = new User
        {
            AccountId = account.Id,
            Name = name,
            Phone = phone,
            Status = UserApprovalStatus.Approved,
            ApprovedAt = _dateTime.UtcNow
        };
        _context.Users.Add(user);

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _locationService.LogLocationAsync(
                account.Id,
                request.Latitude,
                request.Longitude,
                string.IsNullOrWhiteSpace(request.DeviceType) ? "Desktop" : request.DeviceType,
                "RegisterCustomer",
                null,
                null,
                cancellationToken);
        }
        catch
        {
            // Ignore location logging failures
        }

        await _notifications.NotifyAdminsAsync(
            NotificationType.NewCustomer,
            "New Customer Registered",
            $"A new customer has registered: {name} ({email}).",
            NotificationEntityType.Customer,
            user.Id,
            "/customers",
            $"NewCustomer:{user.Id:N}",
            cancellationToken);

        return Result.Success(new RegisterCustomerResponse(
            account.Id,
            name,
            account.Email,
            account.RoleType.ToString()));
    }
}
