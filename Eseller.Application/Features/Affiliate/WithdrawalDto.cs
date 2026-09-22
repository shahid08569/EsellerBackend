namespace Eseller.Application.Features.Affiliate;

public record WithdrawalDto(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    string? PayoutMethod,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessedAt,
    DateTime CreatedAt);