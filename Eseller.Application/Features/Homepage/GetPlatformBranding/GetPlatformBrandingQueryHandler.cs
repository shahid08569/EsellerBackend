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
    public const string SiteTitleKey = "Branding.SiteTitle";
    public const string MetaDescriptionKey = "Branding.MetaDescription";
    public const string FaviconUrlKey = "Branding.FaviconUrl";

    public const string DefaultNavLogo = "/brand/eseller-global-nav.png?v=orange3";
    public const string DefaultFooterLogo = "/brand/eseller-global-logo.png?v=orange3";
    public const string DefaultTagline = "Shop Without Borders";
    public const string DefaultSiteTitle = "eSeller Global — Shop Without Borders | Multi-Vendor Marketplace";
    public const string DefaultMetaDescription =
        "Shop Without Borders on eSeller Global — premier multi-vendor marketplace for electronics, fashion, lifestyle, and home products from verified merchants worldwide.";

    private readonly IApplicationDbContext _context;

    public GetPlatformBrandingQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PlatformBrandingDto>> Handle(
        GetPlatformBrandingQuery request,
        CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            NavLogoKey, FooterLogoKey, TaglineKey,
            SiteTitleKey, MetaDescriptionKey, FaviconUrlKey
        };
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
        var siteTitle = ValueOf(SiteTitleKey);
        var metaDescription = ValueOf(MetaDescriptionKey);
        var favicon = ValueOf(FaviconUrlKey);

        var navLogo = string.IsNullOrWhiteSpace(nav) ? DefaultNavLogo : nav!;
        // Favicon defaults to SuperAdmin navbar logo so Google matches storefront branding.
        var faviconUrl = string.IsNullOrWhiteSpace(favicon) ? navLogo : favicon!;

        return Result.Success(new PlatformBrandingDto(
            navLogo,
            string.IsNullOrWhiteSpace(footer) ? DefaultFooterLogo : footer!,
            string.IsNullOrWhiteSpace(tagline) ? DefaultTagline : tagline!,
            string.IsNullOrWhiteSpace(siteTitle) ? DefaultSiteTitle : siteTitle!,
            string.IsNullOrWhiteSpace(metaDescription) ? DefaultMetaDescription : metaDescription!,
            faviconUrl));
    }
}
