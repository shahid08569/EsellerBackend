using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.GetWithdrawals;

public class GetWithdrawalsQueryHandler
    : IRequestHandler<GetWithdrawalsQuery, Result<PagedList<AdminWithdrawalDto>>>
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

    public async Task<Result<PagedList<AdminWithdrawalDto>>> Handle(
        GetWithdrawalsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AdminWithdrawalDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // 2. Role check — SuperAdmin/Partner only
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<AdminWithdrawalDto>>(
                "Only admins can view withdrawals.", "FORBIDDEN");

        // 3. Query
        var query = _context.Withdrawals.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<WithdrawalStatus>(request.Status, true, out var status))
        {
            query = query.Where(w => w.Status == status);
        }

        if (request.AccountId.HasValue)
            query = query.Where(w => w.AccountId == request.AccountId.Value);

        query = query.OrderByDescending(w => w.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var withdrawals = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 4. Load accounts + affiliates
        var accountIds = withdrawals.Select(w => w.AccountId).Distinct().ToList();

        var affiliates = await _context.Affiliates
            .Where(a => accountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId, a => a, cancellationToken);

        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Email ?? string.Empty, cancellationToken);

        // 5. Build result
        var result = withdrawals.Select(w =>
        {
            var affiliate = affiliates.GetValueOrDefault(w.AccountId);
            var email = accounts.GetValueOrDefault(w.AccountId, string.Empty);

            return new AdminWithdrawalDto(
                w.Id,
                w.AccountId,
                email,
                affiliate?.ReferralCode ?? string.Empty,
                w.Amount,
                w.PayoutMethod,
                w.Status.ToString(),
                w.RequestedAt,
                w.ProcessedAt,
                w.CreatedAt);
        }).ToList();

        var pagedList = new PagedList<AdminWithdrawalDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}