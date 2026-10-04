using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.UpdateProfile;

public class UpdateProfileCommandHandler
    : IRequestHandler<UpdateProfileCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User && role != RoleType.Shopkeeper)
            return Result.Failure("Only customers and sellers can update profile.", "FORBIDDEN");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == _currentUserService.AccountId.Value, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Name is required.", "VALIDATION_ERROR");

        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (role == RoleType.User)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);

            if (user is null)
                return Result.Failure("User not found.", "USER_NOT_FOUND");

            var phoneChanged = !string.Equals(
                NormalizePhone(user.Phone), NormalizePhone(phone), StringComparison.Ordinal);

            user.Name = name;
            user.Phone = phone;
            user.SetUpdatedAt(_dateTime.UtcNow);

            if (phoneChanged)
                account.IsPhoneVerified = false;
        }
        else
        {
            var keeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.AccountId == account.Id, cancellationToken);

            if (keeper is null)
                return Result.Failure("Seller profile not found.", "SHOPKEEPER_NOT_FOUND");

            var phoneChanged = !string.Equals(
                NormalizePhone(keeper.Phone), NormalizePhone(phone), StringComparison.Ordinal);

            keeper.Name = name;
            if (phone is not null)
                keeper.Phone = phone;
            keeper.SetUpdatedAt(_dateTime.UtcNow);

            // Keep shop contact phone in sync when provided
            if (phone is not null)
            {
                var shop = await _context.Shops
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.ShopkeeperId == keeper.Id && s.DeletedAt == null, cancellationToken);
                if (shop is not null)
                {
                    shop.Phone = phone;
                    shop.SetUpdatedAt(_dateTime.UtcNow);
                }
            }

            if (phoneChanged)
                account.IsPhoneVerified = false;
        }

        account.SetUpdatedAt(_dateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static string NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? string.Empty : phone.Trim();
}
