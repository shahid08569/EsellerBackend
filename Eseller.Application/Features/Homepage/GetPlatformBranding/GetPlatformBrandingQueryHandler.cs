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

    public const string FacebookUrlKey = "Social.FacebookUrl";
    public const string TwitterUrlKey = "Social.TwitterUrl";
    public const string InstagramUrlKey = "Social.InstagramUrl";
    public const string YoutubeUrlKey = "Social.YoutubeUrl";
    public const string LinkedinUrlKey = "Social.LinkedinUrl";
    public const string WhatsappUrlKey = "Social.WhatsappUrl";

    public async Task<Result<PlatformBrandingDto>> Handle(
        GetPlatformBrandingQuery request,
        CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            NavLogoKey, FooterLogoKey, TaglineKey,
            SiteTitleKey, MetaDescriptionKey, FaviconUrlKey,
            FacebookUrlKey, TwitterUrlKey, InstagramUrlKey,
            YoutubeUrlKey, LinkedinUrlKey, WhatsappUrlKey
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
        var faviconUrl = string.IsNullOrWhiteSpace(favicon) ? "/favicon.ico" : favicon!;

        var resolvedTitle = string.IsNullOrWhiteSpace(siteTitle) || siteTitle == "EsellerGlobal" || siteTitle == "Eseller — Home"
            ? DefaultSiteTitle
            : siteTitle!;

        return Result.Success(new PlatformBrandingDto(
            navLogo,
            string.IsNullOrWhiteSpace(footer) ? DefaultFooterLogo : footer!,
            string.IsNullOrWhiteSpace(tagline) ? DefaultTagline : tagline!,
            resolvedTitle,
            string.IsNullOrWhiteSpace(metaDescription) ? DefaultMetaDescription : metaDescription!,
            faviconUrl,
            ValueOf(FacebookUrlKey),
            ValueOf(TwitterUrlKey),
            ValueOf(InstagramUrlKey),
            ValueOf(YoutubeUrlKey),
            ValueOf(LinkedinUrlKey),
            ValueOf(WhatsappUrlKey)));
    }
}
