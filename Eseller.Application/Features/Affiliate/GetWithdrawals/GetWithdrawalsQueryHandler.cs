using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetWithdrawals;

public class GetWithdrawalsQueryHandler
    : IRequestHandler<GetWithdrawalsQuery, Result<PagedList<WithdrawalDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetWithdrawalsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<WithdrawalDto>>> Handle(
        GetWithdrawalsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<WithdrawalDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<PagedList<WithdrawalDto>>(
                "Only affiliates can access withdrawals.", "FORBIDDEN");

        var accountId = _currentUserService.AccountId.Value;

        var query = _context.Withdrawals
            .Where(w => w.AccountId == accountId);

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<WithdrawalStatus>(request.Status, true, out var status))
        {
            query = query.Where(w => w.Status == status);
        }

        query = query.OrderByDescending(w => w.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var withdrawals = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = withdrawals.Select(w => new WithdrawalDto(
            w.Id,
            w.AccountId,
            w.Amount,
            w.PayoutMethod,
            w.Status.ToString(),
            w.RequestedAt,
            w.ProcessedAt,
            w.CreatedAt)).ToList();

        var pagedList = new PagedList<WithdrawalDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}