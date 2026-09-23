using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Banners.GetBannerById;

public class GetBannerByIdQueryHandler
    : IRequestHandler<GetBannerByIdQuery, Result<HomepageBannerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetBannerByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<HomepageBannerDto>> Handle(
        GetBannerByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<HomepageBannerDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<HomepageBannerDto>(
                "Only admins can view banners.", "FORBIDDEN");

        var banner = await _context.HomepageBanners
            .FirstOrDefaultAsync(b => b.Id == request.BannerId, cancellationToken);

        if (banner is null)
            return Result.Failure<HomepageBannerDto>(
                "Banner not found.", "BANNER_NOT_FOUND");

        var now = _dateTime.UtcNow;
        var isCurrentlyActive = banner.IsActive
            && (!banner.StartDate.HasValue || banner.StartDate.Value <= now)
            && (!banner.EndDate.HasValue || banner.EndDate.Value >= now);

        var dto = new HomepageBannerDto(
            banner.Id,
            banner.ImageUrl,
            banner.LinkUrl,
            banner.SortOrder,
            banner.IsActive,
            isCurrentlyActive,
            banner.StartDate,
            banner.EndDate,
            banner.CreatedAt,
            banner.UpdatedAt);

        return Result.Success(dto);
    }
}