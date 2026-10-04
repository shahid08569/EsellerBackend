using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Banners.GetHomepageBanners;

public class GetHomepageBannersQueryHandler
    : IRequestHandler<GetHomepageBannersQuery, Result<PagedList<HomepageBannerDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetHomepageBannersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<HomepageBannerDto>>> Handle(
        GetHomepageBannersQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<HomepageBannerDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<HomepageBannerDto>>(
                "Only admins can view banners.", "FORBIDDEN");

        var query = _context.HomepageBanners.AsQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(b => b.IsActive == request.IsActive.Value);

        query = query.OrderBy(b => b.SortOrder).ThenByDescending(b => b.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var banners = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var now = _dateTime.UtcNow;

        var result = banners.Select(b =>
        {
            var isCurrentlyActive = b.IsActive
                && (!b.StartDate.HasValue || b.StartDate.Value <= now)
                && (!b.EndDate.HasValue || b.EndDate.Value >= now);

            return new HomepageBannerDto(
                b.Id,
                b.ImageUrl,
                b.LinkUrl,
                b.Title,
                b.Subtitle,
                b.Description,
                b.Price,
                b.OriginalPrice,
                b.ButtonText,
                b.BackgroundColor,
                b.TextColor,
                b.AvailableSizes,
                b.SortOrder,
                b.IsActive,
                isCurrentlyActive,
                b.StartDate,
                b.EndDate,
                b.CreatedAt,
                b.UpdatedAt);
        }).ToList();

        var pagedList = new PagedList<HomepageBannerDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}