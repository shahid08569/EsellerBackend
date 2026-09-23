using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.UpdateBanner;

public record UpdateBannerCommand(
    Guid BannerId,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTime? StartDate,
    DateTime? EndDate) : IRequest<Result>;