using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Banners.DeleteBanner;

public record DeleteBannerCommand(Guid BannerId) : IRequest<Result>;