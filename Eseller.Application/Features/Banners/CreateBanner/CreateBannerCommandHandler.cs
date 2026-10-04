using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Content;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Banners.CreateBanner;

public class CreateBannerCommandHandler
    : IRequestHandler<CreateBannerCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateBannerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateBannerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>(
                "Only admins can create banners.", "FORBIDDEN");

        var now = _dateTime.UtcNow;

        var banner = new HomepageBanner
        {
            ImageUrl = request.ImageUrl.Trim(),
            LinkUrl = request.LinkUrl?.Trim(),
            Title = request.Title?.Trim(),
            Subtitle = request.Subtitle?.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            OriginalPrice = request.OriginalPrice,
            ButtonText = request.ButtonText?.Trim(),
            BackgroundColor = request.BackgroundColor?.Trim(),
            TextColor = request.TextColor?.Trim(),
            AvailableSizes = request.AvailableSizes?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        banner.SetCreatedAt(now);
        banner.SetUpdatedAt(now);

        _context.HomepageBanners.Add(banner);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(banner.Id);
    }
}