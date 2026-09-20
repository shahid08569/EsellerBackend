using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Coupons.ValidateCoupon;

public record ValidateCouponQuery(
    string Code,
    decimal OrderAmount) : IRequest<Result<CouponValidationResult>>;

public record CouponValidationResult(
    Guid CouponId,
    string Code,
    bool IsValid,
    string? ErrorMessage,
    decimal DiscountAmount);