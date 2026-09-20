using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Coupons.CreateCoupon;

public record CreateCouponCommand(
    string Code,
    DiscountType Type,
    decimal Value,
    decimal? MinOrderAmount,
    decimal? MaxDiscount,
    DateTime? ExpiryDate,
    int? UsageLimit,
    CouponScopeType ScopeType,
    List<Guid>? ProductIds,
    List<Guid>? CategoryIds,
    List<Guid>? ShopIds,
    List<Guid>? CustomerIds) : IRequest<Result<Guid>>;