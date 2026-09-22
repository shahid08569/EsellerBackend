using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetAffiliateConversionStats;

public class GetAffiliateConversionStatsQueryHandler
    : IRequestHandler<GetAffiliateConversionStatsQuery, Result<AffiliateConversionStatsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetAffiliateConversionStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<AffiliateConversionStatsDto>> Handle(
        GetAffiliateConversionStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<AffiliateConversionStatsDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<AffiliateConversionStatsDto>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<AffiliateConversionStatsDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        var last30 = _dateTime.UtcNow.AddDays(-30);

        var commissions = await _context.AffiliateCommissions
            .Where(c => c.AffiliateId == affiliate.Id)
            .ToListAsync(cancellationToken);

        var pendingAmount = commissions
            .Where(c => c.Status == AffiliateCommissionStatus.Pending)
            .Sum(c => c.CommissionAmount);

        var approvedAmount = commissions
            .Where(c => c.Status == AffiliateCommissionStatus.Approved)
            .Sum(c => c.CommissionAmount);

        var paidAmount = commissions
            .Where(c => c.Status == AffiliateCommissionStatus.Paid)
            .Sum(c => c.CommissionAmount);

        var reversedAmount = commissions
            .Where(c => c.Status == AffiliateCommissionStatus.Reversed)
            .Sum(c => c.CommissionAmount);

        var validCommissions = commissions
            .Where(c => c.Status != AffiliateCommissionStatus.Reversed)
            .ToList();

        var conversionsLast30 = validCommissions.Count(c => c.CreatedAt >= last30);

        var earnedLast30 = validCommissions
            .Where(c => c.CreatedAt >= last30)
            .Sum(c => c.CommissionAmount);

        var dto = new AffiliateConversionStatsDto(
            validCommissions.Count,
            affiliate.TotalEarned,
            pendingAmount,
            approvedAmount,
            paidAmount,
            reversedAmount,
            conversionsLast30,
            earnedLast30);

        return Result.Success(dto);
    }
}