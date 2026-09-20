using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Coupons.DeleteCoupon;

public record DeleteCouponCommand(Guid CouponId) : IRequest<Result>;