using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.CreateBanner;

public record CreateBannerCommand(
    string ImageUrl,
    string? LinkUrl,
    string? Title,
    string? Subtitle,
    string? Description,
    decimal? Price,
    decimal? OriginalPrice,
    string? ButtonText,
    string? BackgroundColor,
    string? TextColor,
    string? AvailableSizes,
    int SortOrder,
    bool IsActive,
    DateTime? StartDate,
    DateTime? EndDate) : IRequest<Result<Guid>>;