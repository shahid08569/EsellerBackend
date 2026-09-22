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

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure<UserProfileDto>("Only customers can access profile.", "FORBIDDEN");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == _currentUserService.AccountId.Value,
                cancellationToken);

        if (account is null)
            return Result.Failure<UserProfileDto>("Account not found.", "ACCOUNT_NOT_FOUND");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);

        if (user is null)
            return Result.Failure<UserProfileDto>("User not found.", "USER_NOT_FOUND");

        var dto = new UserProfileDto(
            account.Id,
            user.Id,
            user.Name,
            account.Email,
            user.Phone,
            account.IsEmailVerified,
            account.IsPhoneVerified,
            account.LastLoginAt,
            user.CreatedAt);

        return Result.Success(dto);
    }
}