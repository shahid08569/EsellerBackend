using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.GetProfile;

public class GetProfileQueryHandler
    : IRequestHandler<GetProfileQuery, Result<UserProfileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetProfileQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserProfileDto>> Handle(
        GetProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<UserProfileDto>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User && role != RoleType.Shopkeeper)
            return Result.Failure<UserProfileDto>("Profile not available for this role.", "FORBIDDEN");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == _currentUserService.AccountId.Value,
                cancellationToken);

        if (account is null)
            return Result.Failure<UserProfileDto>("Account not found.", "ACCOUNT_NOT_FOUND");

        if (role == RoleType.User)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);

            if (user is null)
                return Result.Failure<UserProfileDto>("User not found.", "USER_NOT_FOUND");

            return Result.Success(new UserProfileDto(
                account.Id,
                user.Id,
                user.Name,
                account.Email,
                user.Phone,
                account.IsEmailVerified,
                account.IsPhoneVerified,
                account.LastLoginAt,
                user.CreatedAt,
                user.Status.ToString()));
        }

        var keeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == account.Id, cancellationToken);

        if (keeper is null)
            return Result.Failure<UserProfileDto>("Seller profile not found.", "SHOPKEEPER_NOT_FOUND");

        return Result.Success(new UserProfileDto(
            account.Id,
            keeper.Id,
            keeper.Name,
            account.Email,
            keeper.Phone,
            account.IsEmailVerified,
            account.IsPhoneVerified,
            account.LastLoginAt,
            keeper.CreatedAt,
            keeper.Status.ToString()));
    }
}
