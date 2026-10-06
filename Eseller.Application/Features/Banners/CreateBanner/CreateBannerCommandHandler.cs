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

        static string? Clip(string? value, int max) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : (value.Trim().Length <= max ? value.Trim() : value.Trim()[..max]);

        var banner = new HomepageBanner
        {
            ImageUrl = request.ImageUrl.Trim(),
            LinkUrl = Clip(request.LinkUrl, 500),
            Title = Clip(request.Title, 200),
            Subtitle = Clip(request.Subtitle, 200),
            Description = Clip(request.Description, 1000),
            Price = request.Price,
            OriginalPrice = request.OriginalPrice,
            ButtonText = Clip(request.ButtonText, 100),
            BackgroundColor = Clip(request.BackgroundColor, 300),
            TextColor = Clip(request.TextColor, 50),
            AvailableSizes = Clip(request.AvailableSizes, 100),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        banner.SetCreatedAt(now);
        banner.SetUpdatedAt(now);

        _context.HomepageBanners.Add(banner);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(
                $"Could not save banner: {ex.InnerException?.Message ?? ex.Message}",
                "BANNER_SAVE_FAILED");
        }

        return Result.Success(banner.Id);
    }
}