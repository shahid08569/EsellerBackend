using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Discounts.GetDiscounts;

public record GetDiscountsQuery(
    Guid? ProductId = null,
    DiscountScopeType? ScopeType = null,
    Guid? ScopeId = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<DiscountListDto>>>;