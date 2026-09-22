using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetWalletTransactions;

public record GetWalletTransactionsQuery(
    string? Direction = null,
    string? TransactionType = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<WalletTransactionDto>>>;