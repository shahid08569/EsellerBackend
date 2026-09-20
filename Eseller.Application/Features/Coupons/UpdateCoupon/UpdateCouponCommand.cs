using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Coupons.UpdateCoupon;

public record UpdateCouponCommand(
    Guid CouponId,
    decimal Value,
    decimal? MinOrderAmount,
    decimal? MaxDiscount,
    DateTime? ExpiryDate,
    int? UsageLimit,
    bool IsActive) : IRequest<Result>;