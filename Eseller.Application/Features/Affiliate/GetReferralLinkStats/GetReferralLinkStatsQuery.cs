using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetReferralLinkStats;

public record GetReferralLinkStatsQuery : IRequest<Result<ReferralStatsDto>>;

public record ReferralStatsDto(
    string ReferralCode,
    int TotalClicks,
    int TotalConversions,
    decimal TotalEarned,
    decimal ConversionRate,
    int ClicksLast30Days,
    int ConversionsLast30Days,
    decimal EarnedLast30Days);