using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.GetAffiliateCommissions;

public class GetAffiliateCommissionsQueryHandler
    : IRequestHandler<GetAffiliateCommissionsQuery, Result<PagedList<AffiliateCommissionDto>>>
{
    private const string HoldDaysSettingKey = "AffiliateCommissionHoldDays";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetAffiliateCommissionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<AffiliateCommissionDto>>> Handle(
        GetAffiliateCommissionsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AffiliateCommissionDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // 2. Role check
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<AffiliateCommissionDto>>(
                "Only admins can view commissions.", "FORBIDDEN");

        // 3. Query
        var query = _context.AffiliateCommissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<AffiliateCommissionStatus>(request.Status, true, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (request.AffiliateId.HasValue)
            query = query.Where(c => c.AffiliateId == request.AffiliateId.Value);

        query = query.OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var commissions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 4. Load affiliates
        var affiliateIds = commissions.Select(c => c.AffiliateId).Distinct().ToList();

        var affiliates = await _context.Affiliates
            .Where(a => affiliateIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a, cancellationToken);

        var accountIds = affiliates.Values.Select(a => a.AccountId).Distinct().ToList();

        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Email ?? string.Empty, cancellationToken);

        // 5. Load delivered dates
        var now = _dateTime.UtcNow;
        var orderIds = commissions.Select(c => c.OrderRequestId).Distinct().ToList();

        var deliveredHistories = await _context.OrderStatusHistories
            .Where(h => orderIds.Contains(h.OrderRequestId)
                     && h.NewStatus == OrderStatus.Delivered)
            .ToDictionaryAsync(h => h.OrderRequestId, h => h.ChangedAt, cancellationToken);

        // 6. Load hold days from Settings
        var holdDaysSetting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == HoldDaysSettingKey, cancellationToken);

        int holdDays = 7;
        if (holdDaysSetting is not null && int.TryParse(holdDaysSetting.Value, out var parsed))
            holdDays = parsed;

        // 7. Build result
        var result = commissions.Select(c =>
        {
            var affiliate = affiliates.GetValueOrDefault(c.AffiliateId);
            var email = affiliate is not null
                ? accounts.GetValueOrDefault(affiliate.AccountId, string.Empty)
                : string.Empty;

            DateTime? deliveredAt = deliveredHistories.TryGetValue(c.OrderRequestId, out var dt)
                ? dt
                : null;

            DateTime? availableAt = deliveredAt?.AddDays(holdDays);
            var isAvailable = availableAt.HasValue && availableAt.Value <= now;

            return new AffiliateCommissionDto(
                c.Id,
                c.AffiliateId,
                affiliate?.ReferralCode ?? string.Empty,
                email,
                c.OrderRequestId,
                c.OrderRequestItemId,
                c.AffiliateClickId,
                c.CommissionAmount,
                c.Status.ToString(),
                c.CreatedAt,
                c.ApprovedAt,
                c.PaidAt,
                deliveredAt,
                availableAt,
                isAvailable);
        }).ToList();

        var pagedList = new PagedList<AffiliateCommissionDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}