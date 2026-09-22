using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetReferralCode;

public class GetReferralCodeQueryHandler
    : IRequestHandler<GetReferralCodeQuery, Result<ReferralCodeDto>>
{
    private const string BaseUrl = "https://eseller.com";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetReferralCodeQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ReferralCodeDto>> Handle(
        GetReferralCodeQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<ReferralCodeDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<ReferralCodeDto>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<ReferralCodeDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        if (!affiliate.IsActive)
            return Result.Failure<ReferralCodeDto>(
                "Affiliate account is not active.", "AFFILIATE_INACTIVE");

        var referralLink = $"{BaseUrl}?ref={affiliate.ReferralCode}";

        var dto = new ReferralCodeDto(
            affiliate.ReferralCode,
            referralLink,
            affiliate.TotalClicks,
            affiliate.TotalConversions,
            affiliate.TotalEarned,
            affiliate.CreatedAt);

        return Result.Success(dto);
    }
}