using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Banners.UpdateBanner;

public class UpdateBannerCommandHandler
    : IRequestHandler<UpdateBannerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateBannerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateBannerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can update banners.", "FORBIDDEN");

        var banner = await _context.HomepageBanners
            .FirstOrDefaultAsync(b => b.Id == request.BannerId, cancellationToken);

        if (banner is null)
            return Result.Failure("Banner not found.", "BANNER_NOT_FOUND");

        banner.ImageUrl = request.ImageUrl.Trim();
        banner.LinkUrl = request.LinkUrl?.Trim();
        banner.SortOrder = request.SortOrder;
        banner.IsActive = request.IsActive;
        banner.StartDate = request.StartDate;
        banner.EndDate = request.EndDate;
        banner.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}