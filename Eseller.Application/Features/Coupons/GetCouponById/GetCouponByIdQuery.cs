using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Coupons.GetCouponById;

public record GetCouponByIdQuery(Guid CouponId) : IRequest<Result<CouponDto>>;