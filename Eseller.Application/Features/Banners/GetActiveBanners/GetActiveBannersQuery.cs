using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.GetActiveBanners;

public record GetActiveBannersQuery : IRequest<Result<List<HomepageBannerDto>>>;