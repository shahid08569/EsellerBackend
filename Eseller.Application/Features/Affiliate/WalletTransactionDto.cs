namespace Eseller.Application.Features.Affiliate;

public record WalletTransactionDto(
    Guid Id,
    Guid WalletId,
    string Direction,
    string TransactionType,
    decimal Amount,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Description,
    DateTime CreatedAt);