namespace Eseller.Application.Features.Affiliate;

public record WalletDto(
    Guid Id,
    Guid AccountId,
    decimal Balance,
    DateTime? LastTransactionAt,
    DateTime CreatedAt);