using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetAffiliateWallet;

public class GetAffiliateWalletQueryHandler
    : IRequestHandler<GetAffiliateWalletQuery, Result<WalletDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAffiliateWalletQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<WalletDto>> Handle(
        GetAffiliateWalletQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<WalletDto>("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<WalletDto>("Only affiliates can access wallet.", "FORBIDDEN");

        var accountId = _currentUserService.AccountId.Value;

        // Ensure affiliate exists
        var affiliateExists = await _context.Affiliates
            .AnyAsync(a => a.AccountId == accountId, cancellationToken);

        if (!affiliateExists)
            return Result.Failure<WalletDto>("Affiliate not found.", "AFFILIATE_NOT_FOUND");

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.AccountId == accountId, cancellationToken);

        if (wallet is null)
        {
            // Return empty wallet (0 balance)
            return Result.Success(new WalletDto(
                Guid.Empty,
                accountId,
                0m,
                null,
                DateTime.UtcNow));
        }

        var lastTransaction = await _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (DateTime?)t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new WalletDto(
            wallet.Id,
            wallet.AccountId,
            wallet.Balance,
            lastTransaction,
            wallet.CreatedAt);

        return Result.Success(dto);
    }
}