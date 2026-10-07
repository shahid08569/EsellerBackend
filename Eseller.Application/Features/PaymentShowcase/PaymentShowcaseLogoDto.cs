namespace Eseller.Application.Features.PaymentShowcase;

public record PaymentShowcaseLogoDto(
    Guid Id,
    string Name,
    string ImageUrl,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
