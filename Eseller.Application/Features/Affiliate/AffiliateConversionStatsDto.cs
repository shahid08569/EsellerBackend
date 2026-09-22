namespace Eseller.Application.Features.Affiliate;

public record AffiliateConversionStatsDto(
    int TotalConversions,
    decimal TotalEarned,
    decimal PendingAmount,
    decimal ApprovedAmount,
    decimal PaidAmount,
    decimal ReversedAmount,
    int ConversionsLast30Days,
    decimal EarnedLast30Days);