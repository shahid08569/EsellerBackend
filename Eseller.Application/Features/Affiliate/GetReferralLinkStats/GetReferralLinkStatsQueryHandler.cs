using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetReferralLinkStats;

public class GetReferralLinkStatsQueryHandler
    : IRequestHandler<GetReferralLinkStatsQuery, Result<ReferralStatsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetReferralLinkStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<ReferralStatsDto>> Handle(
        GetReferralLinkStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<ReferralStatsDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<ReferralStatsDto>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<ReferralStatsDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        if (!affiliate.IsActive)
            return Result.Failure<ReferralStatsDto>(
                "Affiliate account is not active.", "AFFILIATE_INACTIVE");

        var last30Days = _dateTime.UtcNow.AddDays(-30);

        var clicksLast30 = await _context.AffiliateClicks
            .Where(c => c.AffiliateId == affiliate.Id && c.ClickedAt >= last30Days)
            .CountAsync(cancellationToken);

        var conversionsLast30 = await _context.AffiliateCommissions
            .Where(c => c.AffiliateId == affiliate.Id
                     && c.CreatedAt >= last30Days
                     && c.Status != AffiliateCommissionStatus.Reversed)
            .CountAsync(cancellationToken);

        var earnedLast30 = await _context.AffiliateCommissions
            .Where(c => c.AffiliateId == affiliate.Id
                     && c.CreatedAt >= last30Days
                     && c.Status != AffiliateCommissionStatus.Reversed)
            .SumAsync(c => (decimal?)c.CommissionAmount, cancellationToken) ?? 0;

        var conversionRate = affiliate.TotalClicks > 0
            ? (decimal)affiliate.TotalConversions / affiliate.TotalClicks * 100
            : 0;

        var dto = new ReferralStatsDto(
            affiliate.ReferralCode,
            affiliate.TotalClicks,
            affiliate.TotalConversions,
            affiliate.TotalEarned,
            Math.Round(conversionRate, 2),
            clicksLast30,
            conversionsLast30,
            earnedLast30);

        return Result.Success(dto);
    }
}