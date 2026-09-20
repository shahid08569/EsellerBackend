namespace Eseller.Application.Features.Coupons;

public record CouponDto(
    Guid Id,
    string Code,
    string Type,
    decimal Value,
    decimal? MinOrderAmount,
    decimal? MaxDiscount,
    DateTime? ExpiryDate,
    int? UsageLimit,
    int UsedCount,
    string ScopeType,
    Guid? CreatedByAccountId,
    bool IsActive,
    bool IsCurrentlyValid,
    DateTime CreatedAt);