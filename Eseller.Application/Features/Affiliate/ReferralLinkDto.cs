namespace Eseller.Application.Features.Affiliate;

public record ReferralLinkDto(
    string ReferralCode,
    string BaseLink,
    string? ProductLink,
    Guid? ProductId,
    string? ProductName,
    string? ProductSlug);