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
    DateTime CreatedAt,
    string? UserName = null,
    string? UserType = null,
    string? StoreName = null,
    string? PaymentMethod = null,
    string? AccountDetails = null,
    bool HasBankDetails = false);