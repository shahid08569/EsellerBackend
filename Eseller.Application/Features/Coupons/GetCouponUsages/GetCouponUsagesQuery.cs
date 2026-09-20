using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Coupons.GetCouponUsages;

public record GetCouponUsagesQuery(
    Guid CouponId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<CouponUsageDto>>>;