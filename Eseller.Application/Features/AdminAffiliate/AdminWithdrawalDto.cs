namespace Eseller.Application.Features.AdminAffiliate;

public record AdminWithdrawalDto(
    Guid Id,
    Guid AccountId,
    string AffiliateEmail,
    string AffiliateReferralCode,
    decimal Amount,
    string? PayoutMethod,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessedAt,
    DateTime CreatedAt);