namespace Eseller.Application.Features.Coupons;

public record CouponUsageDto(
    Guid Id,
    Guid CouponId,
    string CouponCode,
    Guid OrderRequestId,
    Guid UserId,
    string UserEmail,
    DateTime UsedAt);