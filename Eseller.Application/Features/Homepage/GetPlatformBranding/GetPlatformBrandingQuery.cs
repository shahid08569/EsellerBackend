using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetPlatformBranding;

public record PlatformBrandingDto(
    string NavLogoUrl,
    string FooterLogoUrl,
    string Tagline);

public record GetPlatformBrandingQuery() : IRequest<Result<PlatformBrandingDto>>;
