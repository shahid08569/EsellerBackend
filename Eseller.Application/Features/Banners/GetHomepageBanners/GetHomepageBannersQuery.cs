using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.GetHomepageBanners;

public record GetHomepageBannersQuery(
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedList<HomepageBannerDto>>>;