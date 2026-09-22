namespace Eseller.Application.Features.Affiliate;

public record AffiliateClickStatsDto(
    int TotalClicks,
    int TotalConversions,
    decimal ConversionRate,
    int ClicksLast7Days,
    int ClicksLast30Days,
    int ConversionsLast7Days,
    int ConversionsLast30Days);