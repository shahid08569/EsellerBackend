using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetAffiliateConversions;

public record GetAffiliateConversionsQuery(
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<AffiliateConversionDto>>>;