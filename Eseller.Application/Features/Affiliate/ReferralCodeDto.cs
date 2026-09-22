namespace Eseller.Application.Features.Affiliate;

public record ReferralCodeDto(
    string ReferralCode,
    string ReferralLink,
    int TotalClicks,
    int TotalConversions,
    decimal TotalEarned,
    DateTime CreatedAt);