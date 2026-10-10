using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetPlatformBranding;

public record PlatformBrandingDto(
    string NavLogoUrl,
    string FooterLogoUrl,
    string Tagline,
    string SiteTitle,
    string MetaDescription,
    string FaviconUrl,
    string? FacebookUrl = null,
    string? TwitterUrl = null,
    string? InstagramUrl = null,
    string? YoutubeUrl = null,
    string? LinkedinUrl = null,
    string? WhatsappUrl = null);

public record GetPlatformBrandingQuery() : IRequest<Result<PlatformBrandingDto>>;
