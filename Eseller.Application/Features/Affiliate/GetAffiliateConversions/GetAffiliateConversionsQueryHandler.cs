using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetAffiliateConversions;

public class GetAffiliateConversionsQueryHandler
    : IRequestHandler<GetAffiliateConversionsQuery, Result<PagedList<AffiliateConversionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAffiliateConversionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<AffiliateConversionDto>>> Handle(
        GetAffiliateConversionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AffiliateConversionDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<PagedList<AffiliateConversionDto>>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<PagedList<AffiliateConversionDto>>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        var query = _context.AffiliateCommissions
            .Where(c => c.AffiliateId == affiliate.Id);

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<AffiliateCommissionStatus>(request.Status, true, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        query = query.OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var conversions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = conversions.Select(c => new AffiliateConversionDto(
            c.Id,
            c.OrderRequestId,
            c.OrderRequestItemId,
            c.AffiliateClickId,
            c.CommissionAmount,
            c.Status.ToString(),
            c.CreatedAt,
            c.ApprovedAt,
            c.PaidAt)).ToList();

        var pagedList = new PagedList<AffiliateConversionDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}