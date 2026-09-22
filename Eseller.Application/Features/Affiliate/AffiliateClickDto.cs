namespace Eseller.Application.Features.Affiliate;

public record AffiliateClickDto(
    Guid Id,
    Guid? ProductId,
    string? ProductName,
    string? ProductSlug,
    string? IpAddress,
    string? UserAgent,
    bool Converted,
    DateTime ClickedAt);