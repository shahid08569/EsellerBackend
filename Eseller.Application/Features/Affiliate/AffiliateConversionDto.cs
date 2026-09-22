namespace Eseller.Application.Features.Affiliate;

public record AffiliateConversionDto(
    Guid Id,
    Guid OrderRequestId,
    Guid OrderRequestItemId,
    Guid? AffiliateClickId,
    decimal CommissionAmount,
    string Status,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? PaidAt);