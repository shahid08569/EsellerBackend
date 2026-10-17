using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminPartners.GetPartners;

public class GetPartnersQueryHandler
    : IRequestHandler<GetPartnersQuery, Result<PagedList<PartnerDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPartnersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<PartnerDto>>> Handle(
        GetPartnersQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<PartnerDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<PartnerDto>>(
                "Only admins can view partners.", "FORBIDDEN");

        var query = _context.Partners
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var partners = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Load accounts
        var accountIds = partners.Select(p => p.AccountId).Distinct().ToList();
        var addedByIds = partners.Select(p => p.AddedBySuperAdminId).Distinct().ToList();
        var allIds = accountIds.Concat(addedByIds).Distinct().ToList();

        var accounts = await _context.Accounts
            .Where(a => allIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a, cancellationToken);

        var partnerIds = partners.Select(p => p.Id).ToList();
        var shopCounts = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.PartnerId.HasValue && partnerIds.Contains(s.PartnerId.Value) && s.DeletedAt == null)
            .GroupBy(s => s.PartnerId!.Value)
            .Select(g => new { PartnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PartnerId, x => x.Count, cancellationToken);

        var result = partners.Select(p =>
        {
            var account = accounts.GetValueOrDefault(p.AccountId);
            var addedBy = accounts.GetValueOrDefault(p.AddedBySuperAdminId);

            return new PartnerDto(
                p.Id,
                p.AccountId,
                account?.Email ?? string.Empty,
                p.AddedBySuperAdminId,
                addedBy?.Email ?? string.Empty,
                p.AddedAt,
                p.Permissions,
                p.CreatedAt,
                account?.IsActive ?? true,
                p.ReferralCode ?? string.Empty,
                shopCounts.GetValueOrDefault(p.Id, 0));
        }).ToList();

        var pagedList = new PagedList<PartnerDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}