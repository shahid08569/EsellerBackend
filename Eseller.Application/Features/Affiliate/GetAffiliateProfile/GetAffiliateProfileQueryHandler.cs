using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetAffiliateProfile;

public class GetAffiliateProfileQueryHandler
    : IRequestHandler<GetAffiliateProfileQuery, Result<AffiliateProfileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAffiliateProfileQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AffiliateProfileDto>> Handle(
        GetAffiliateProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<AffiliateProfileDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<AffiliateProfileDto>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<AffiliateProfileDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == affiliate.AccountId, cancellationToken);

        if (account is null)
            return Result.Failure<AffiliateProfileDto>(
                "Account not found.", "ACCOUNT_NOT_FOUND");

        var dto = new AffiliateProfileDto(
            affiliate.Id,
            affiliate.AccountId,
            account.Email ?? string.Empty,
            affiliate.ReferralCode,
            affiliate.CommissionRate,
            affiliate.TotalClicks,
            affiliate.TotalConversions,
            affiliate.TotalEarned,
            affiliate.IsActive,
            affiliate.CreatedAt);

        return Result.Success(dto);
    }
}