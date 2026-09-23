using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.GetBannerById;

public record GetBannerByIdQuery(Guid BannerId) : IRequest<Result<HomepageBannerDto>>;