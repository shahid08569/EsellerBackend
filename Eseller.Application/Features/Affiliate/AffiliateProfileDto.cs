namespace Eseller.Application.Features.Affiliate;

public record AffiliateProfileDto(
    Guid Id,
    Guid AccountId,
    string Email,
    string ReferralCode,
    decimal? CommissionRate,
    int TotalClicks,
    int TotalConversions,
    decimal TotalEarned,
    bool IsActive,
    DateTime CreatedAt);