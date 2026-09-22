using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetAffiliateClicks;

public class GetAffiliateClicksQueryHandler
    : IRequestHandler<GetAffiliateClicksQuery, Result<PagedList<AffiliateClickDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAffiliateClicksQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<AffiliateClickDto>>> Handle(
        GetAffiliateClicksQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AffiliateClickDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<PagedList<AffiliateClickDto>>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<PagedList<AffiliateClickDto>>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        var query = _context.AffiliateClicks
            .Where(c => c.AffiliateId == affiliate.Id);

        if (request.ConvertedOnly == true)
            query = query.Where(c => c.Converted);

        if (request.ProductId.HasValue)
            query = query.Where(c => c.ProductId == request.ProductId.Value);

        query = query.OrderByDescending(c => c.ClickedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var clicks = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Load products
        var productIds = clicks
            .Where(c => c.ProductId.HasValue)
            .Select(c => c.ProductId!.Value)
            .Distinct()
            .ToList();

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Slug }, cancellationToken);

        var result = clicks.Select(c =>
        {
            var product = c.ProductId.HasValue
                ? products.GetValueOrDefault(c.ProductId.Value)
                : null;

            return new AffiliateClickDto(
                c.Id,
                c.ProductId,
                product?.Name,
                product?.Slug,
                c.IpAddress,
                c.UserAgent,
                c.Converted,
                c.ClickedAt);
        }).ToList();

        var pagedList = new PagedList<AffiliateClickDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}