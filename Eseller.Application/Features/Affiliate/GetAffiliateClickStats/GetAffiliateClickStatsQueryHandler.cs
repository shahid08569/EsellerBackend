using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetAffiliateClickStats;

public class GetAffiliateClickStatsQueryHandler
    : IRequestHandler<GetAffiliateClickStatsQuery, Result<AffiliateClickStatsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetAffiliateClickStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<AffiliateClickStatsDto>> Handle(
        GetAffiliateClickStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<AffiliateClickStatsDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<AffiliateClickStatsDto>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<AffiliateClickStatsDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        var now = _dateTime.UtcNow;
        var last7 = now.AddDays(-7);
        var last30 = now.AddDays(-30);

        var clicksQuery = _context.AffiliateClicks
            .Where(c => c.AffiliateId == affiliate.Id);

        var clicksLast7 = await clicksQuery
            .CountAsync(c => c.ClickedAt >= last7, cancellationToken);

        var clicksLast30 = await clicksQuery
            .CountAsync(c => c.ClickedAt >= last30, cancellationToken);

        var conversionsLast7 = await clicksQuery
            .CountAsync(c => c.Converted && c.ClickedAt >= last7, cancellationToken);

        var conversionsLast30 = await clicksQuery
            .CountAsync(c => c.Converted && c.ClickedAt >= last30, cancellationToken);

        var conversionRate = affiliate.TotalClicks > 0
            ? (decimal)affiliate.TotalConversions / affiliate.TotalClicks * 100
            : 0;

        var dto = new AffiliateClickStatsDto(
            affiliate.TotalClicks,
            affiliate.TotalConversions,
            Math.Round(conversionRate, 2),
            clicksLast7,
            clicksLast30,
            conversionsLast7,
            conversionsLast30);

        return Result.Success(dto);
    }
}