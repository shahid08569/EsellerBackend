using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Homepage.GetPlatformBranding;

public class GetPlatformBrandingQueryHandler
    : IRequestHandler<GetPlatformBrandingQuery, Result<PlatformBrandingDto>>
{
    public const string NavLogoKey = "Branding.NavLogoUrl";
    public const string FooterLogoKey = "Branding.FooterLogoUrl";
    public const string TaglineKey = "Branding.Tagline";

    public const string DefaultNavLogo = "/brand/eseller-global-nav.png?v=orange3";
    public const string DefaultFooterLogo = "/brand/eseller-global-logo.png?v=orange3";
    public const string DefaultTagline = "Shop Without Borders";

    private readonly IApplicationDbContext _context;

    public GetPlatformBrandingQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PlatformBrandingDto>> Handle(
        GetPlatformBrandingQuery request,
        CancellationToken cancellationToken)
    {
        var keys = new[] { NavLogoKey, FooterLogoKey, TaglineKey };
        var settings = await _context.Settings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key) && s.IsActive)
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(cancellationToken);

        string? ValueOf(string key) =>
            settings.FirstOrDefault(s => s.Key == key)?.Value?.Trim();

        var nav = ValueOf(NavLogoKey);
        var footer = ValueOf(FooterLogoKey);
        var tagline = ValueOf(TaglineKey);

        return Result.Success(new PlatformBrandingDto(
            string.IsNullOrWhiteSpace(nav) ? DefaultNavLogo : nav,
            string.IsNullOrWhiteSpace(footer) ? DefaultFooterLogo : footer,
            string.IsNullOrWhiteSpace(tagline) ? DefaultTagline : tagline));
    }
}
