using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.CreateBanner;

public record CreateBannerCommand(
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    DateTime? StartDate,
    DateTime? EndDate) : IRequest<Result<Guid>>;