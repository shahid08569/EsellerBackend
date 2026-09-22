using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetWalletTransactions;

public class GetWalletTransactionsQueryHandler
    : IRequestHandler<GetWalletTransactionsQuery, Result<PagedList<WalletTransactionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetWalletTransactionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<WalletTransactionDto>>> Handle(
        GetWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<WalletTransactionDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<PagedList<WalletTransactionDto>>(
                "Only affiliates can access wallet.", "FORBIDDEN");

        var accountId = _currentUserService.AccountId.Value;

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);

        if (wallet is null)
            return Result.Success(new PagedList<WalletTransactionDto>(
                new List<WalletTransactionDto>(), 0, request.PageNumber, request.PageSize));

        var query = _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id);

        // Direction filter (Credit/Debit)
        if (!string.IsNullOrWhiteSpace(request.Direction)
            && Enum.TryParse<WalletDirection>(request.Direction, true, out var dir))
        {
            query = query.Where(t => t.Direction == dir);
        }

        // TransactionType filter
        if (!string.IsNullOrWhiteSpace(request.TransactionType)
            && Enum.TryParse<WalletTransactionType>(request.TransactionType, true, out var type))
        {
            query = query.Where(t => t.TransactionType == type);
        }

        query = query.OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = transactions.Select(t => new WalletTransactionDto(
            t.Id,
            t.WalletId,
            t.Direction.ToString(),
            t.TransactionType.ToString(),
            t.Amount,
            t.ReferenceType,
            t.ReferenceId,
            t.Description,
            t.CreatedAt)).ToList();

        var pagedList = new PagedList<WalletTransactionDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}