using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Coupons.GetCoupons;

public record GetCouponsQuery(
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<CouponListDto>>>;