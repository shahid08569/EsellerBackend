using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetAffiliateClicks;

public record GetAffiliateClicksQuery(
    bool? ConvertedOnly = null,
    Guid? ProductId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<AffiliateClickDto>>>;